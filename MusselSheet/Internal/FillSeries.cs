namespace MusselSheet.Internal;

using System.Globalization;

internal static class FillSeries
{
    // Position 0 is the first source value; downward and rightward positions continue from the value count,
    // upward and leftward ones start at -1. A null function means plain copy, and a null result skips that cell.
    public static Func<int, object?>? Create(IReadOnlyList<object?> values, bool toggle)
    {
        if (values.Count == 0)
        {
            return null;
        }

        if (TryNumbers(values, out var numbers))
        {
            // A single number is copied (Ctrl: incremented by 1); two or more form a series (Ctrl: copied)
            return (numbers.Length >= 2) == toggle ? null : NumberSeries(numbers);
        }

        if (toggle)
        {
            return null;
        }

        if (TryDates(values, out var dates))
        {
            return DateSeries(dates);
        }

        return TryNumbered(values, out var parts) ? TextSeries(parts) : null;
    }

    public static int Mod(int position, int count) => ((position % count) + count) % count;

    private static Func<int, object?> NumberSeries(decimal[] numbers)
    {
        if (numbers.Length == 1)
        {
            return position => Line(numbers[0], 1m, position);
        }

        // Equal spacing steps by that interval and keeps the source's digits; otherwise a least-squares line with
        // trailing zeros dropped.
        if (Step(numbers) is { } even)
        {
            return position => Line(numbers[0], even, position);
        }

        var (start, step) = Fit(numbers);
        return position => Line(start, step, position) is decimal value ? Normalize(value) : null;
    }

    private static Func<int, object?> DateSeries(DateOnly[] dates)
    {
        if (dates.Length == 1)
        {
            return position => AddDays(dates[0], position);
        }

        var months = MonthIndex(dates[1]) - MonthIndex(dates[0]);
        var monthly = (months != 0) && Array.TrueForAll(dates, x => x.Day == dates[0].Day);
        for (var i = 2; monthly && (i < dates.Length); i++)
        {
            monthly = MonthIndex(dates[i]) - MonthIndex(dates[i - 1]) == months;
        }

        if (monthly)
        {
            return position => AddMonths(dates[0], (long)months * position);
        }

        var days = Array.ConvertAll(dates, x => (decimal)x.DayNumber);
        if (Step(days) is { } step)
        {
            return position => AddDays(dates[0], step * position);
        }

        var (start, slope) = Fit(days);
        return position => Line(start, slope, position) is decimal day ? FromDayNumber(Math.Round(day, MidpointRounding.AwayFromZero)) : null;
    }

    private static Func<int, object?> TextSeries(Numbered[] parts)
    {
        var same = Array.TrueForAll(parts, x => (x.Prefix == parts[0].Prefix) && (x.Suffix == parts[0].Suffix));
        if (same && (parts.Length >= 2))
        {
            var numbers = Array.ConvertAll(parts, x => (decimal)x.Number);
            var (start, step) = Step(numbers) is { } even ? (numbers[0], even) : Fit(numbers);
            return position => Line(start, step, position) is decimal number ? Format(parts[0], Math.Round(number, MidpointRounding.AwayFromZero)) : null;
        }

        return position =>
        {
            var index = Mod(position, parts.Length);
            var round = (position - index) / parts.Length;
            return Format(parts[index], (decimal)parts[index].Number + round);
        };
    }

    private static bool TryNumbers(IReadOnlyList<object?> values, out decimal[] numbers)
    {
        numbers = new decimal[values.Count];
        for (var i = 0; i < values.Count; i++)
        {
            if (!TryNumber(values[i], out numbers[i]))
            {
                return false;
            }
        }

        return true;
    }

    private static bool TryNumber(object? value, out decimal number)
    {
        number = 0m;
        if (value is not (decimal or int or long or short or byte or sbyte or uint or ulong or ushort or double or float))
        {
            return false;
        }

        try
        {
            number = Convert.ToDecimal(value, CultureInfo.InvariantCulture);
            return true;
        }
        catch (OverflowException)
        {
            return false;
        }
    }

    private static bool TryDates(IReadOnlyList<object?> values, out DateOnly[] dates)
    {
        dates = new DateOnly[values.Count];
        for (var i = 0; i < values.Count; i++)
        {
            switch (values[i])
            {
                case DateOnly date:
                    dates[i] = date;
                    break;
                case DateTime dateTime:
                    dates[i] = DateOnly.FromDateTime(dateTime);
                    break;
                default:
                    return false;
            }
        }

        return true;
    }

    // The last run of half-width digits is the number; runs of 19 or more digits are not treated as numbers
    private static bool TryNumbered(IReadOnlyList<object?> values, out Numbered[] parts)
    {
        parts = new Numbered[values.Count];
        for (var i = 0; i < values.Count; i++)
        {
            if ((values[i] is not string text) || !TrySplit(text, out parts[i]))
            {
                return false;
            }
        }

        return true;
    }

    private static bool TrySplit(string text, out Numbered part)
    {
        part = default;
        var end = text.Length;
        while ((end > 0) && !char.IsAsciiDigit(text[end - 1]))
        {
            end--;
        }

        var start = end;
        while ((start > 0) && char.IsAsciiDigit(text[start - 1]))
        {
            start--;
        }

        if ((start == end) || (end - start > 18))
        {
            return false;
        }

        part = new Numbered(text[..start], long.Parse(text.AsSpan(start, end - start), CultureInfo.InvariantCulture), end - start, text[end..]);
        return true;
    }

    private static decimal? Step(decimal[] values)
    {
        var step = values[1] - values[0];
        for (var i = 2; i < values.Length; i++)
        {
            if (values[i] - values[i - 1] != step)
            {
                return null;
            }
        }

        return step;
    }

    private static (decimal Start, decimal Step) Fit(decimal[] values)
    {
        var count = values.Length;
        var meanX = (count - 1) / 2m;
        var meanY = values.Average();
        var (sxy, sxx) = (0m, 0m);
        for (var i = 0; i < count; i++)
        {
            var dx = i - meanX;
            sxy += dx * (values[i] - meanY);
            sxx += dx * dx;
        }

        var step = sxy / sxx;
        return (meanY - (step * meanX), step);
    }

    private static decimal? Line(decimal start, decimal step, int position)
    {
        try
        {
            return start + (step * position);
        }
        catch (OverflowException)
        {
            return null;
        }
    }

    // Dividing by 1.000... strips trailing zeros (30.0 becomes 30)
    private static decimal Normalize(decimal value) => value / 1.0000000000000000000000000000m;

    private static int MonthIndex(DateOnly date) => (date.Year * 12) + date.Month - 1;

    private static DateOnly? AddDays(DateOnly date, decimal days) => FromDayNumber(date.DayNumber + days);

    private static DateOnly? FromDayNumber(decimal day) =>
        (day >= DateOnly.MinValue.DayNumber) && (day <= DateOnly.MaxValue.DayNumber) ? DateOnly.FromDayNumber((int)day) : null;

    // A day past the end of the month becomes its last day, counted from the original day: 1/31, 2/28, 3/31
    private static DateOnly? AddMonths(DateOnly date, long months)
    {
        var index = MonthIndex(date) + months;
        if ((index < 0) || (index >= 9999 * 12))
        {
            return null;
        }

        var (year, month) = ((int)(index / 12), (int)(index % 12) + 1);
        return new DateOnly(year, month, Math.Min(date.Day, DateTime.DaysInMonth(year, month)));
    }

    // Zero-padded digits keep their width, and a negative number uses its absolute value, same as Excel
    private static string Format(Numbered part, decimal number) =>
        string.Concat(part.Prefix, Math.Abs(number).ToString(new string('0', part.Width), CultureInfo.InvariantCulture), part.Suffix);

    private readonly record struct Numbered(string Prefix, long Number, int Width, string Suffix);
}
