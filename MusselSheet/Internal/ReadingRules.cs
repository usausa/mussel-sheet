namespace MusselSheet.Internal;

using System.Globalization;
using System.Text.RegularExpressions;

internal enum DateOrder
{
    YearMonthDay,
    MonthDayYear,
    DayMonthYear
}

// Separators and date order come from the sheet's CultureInfo (pass a custom one to Sheet.Culture to
// change them), so display, edit text and reading cannot diverge. The JS validation receives the same values.
internal sealed partial record ReadingRules(string DecimalSeparator, string GroupSeparator, string TimeSeparator, DateOrder DateOrder, string EditDateFormat)
{
    public DateRules Dates { get; init; } = DateRules.None;

    public SheetCharacterMap? Readings { get; init; }

    public SheetCharacterMap? DateReadings { get; init; }

    // A culture whose two number separators are the same has no grouping that could be told from the decimal point
    public static ReadingRules From(CultureInfo culture)
    {
        var numbers = culture.NumberFormat;
        var group = numbers.NumberGroupSeparator == numbers.NumberDecimalSeparator ? string.Empty : numbers.NumberGroupSeparator;
        return new(numbers.NumberDecimalSeparator, group, culture.DateTimeFormat.TimeSeparator, OrderOf(culture), EditFormat(culture));
    }

    // A value is always a Gregorian date, whatever calendar the culture has by default
    public static CultureInfo Gregorian(CultureInfo culture)
    {
        if (culture.DateTimeFormat.Calendar is GregorianCalendar)
        {
            return culture;
        }

        var clone = (CultureInfo)culture.Clone();
        clone.DateTimeFormat.Calendar = new GregorianCalendar();
        return clone;
    }

    public static DateOrder OrderOf(CultureInfo culture)
    {
        var pattern = culture.DateTimeFormat.ShortDatePattern;
        var (y, m, d) = (pattern.IndexOf('y', StringComparison.Ordinal), pattern.IndexOf('M', StringComparison.Ordinal), pattern.IndexOf('d', StringComparison.Ordinal));
        if ((y >= 0) && ((m < 0) || (y < m)) && ((d < 0) || (y < d)))
        {
            return DateOrder.YearMonthDay;
        }

        return (d >= 0) && (m >= 0) && (d < m) ? DateOrder.DayMonthYear : DateOrder.MonthDayYear;
    }

    // Edit format for dates that can be read back: the short date pattern with a 4-digit year if it is only
    // digits and / . - separators, otherwise one built from the field order and '/'.
    public static string EditFormat(CultureInfo culture)
    {
        var pattern = culture.DateTimeFormat.ShortDatePattern;
        if (NumericDatePattern().IsMatch(pattern))
        {
            return YearDigits().Replace(pattern, "yyyy");
        }

        return OrderOf(culture) switch
        {
            DateOrder.DayMonthYear => "dd/MM/yyyy",
            DateOrder.MonthDayYear => "MM/dd/yyyy",
            _ => "yyyy/MM/dd"
        };
    }

    public ReadingInfo ForScript() => new(
        DecimalSeparator,
        GroupSeparator,
        TimeSeparator,
        DateOrder switch
        {
            DateOrder.DayMonthYear => "dmy",
            DateOrder.MonthDayYear => "mdy",
            _ => "ymd"
        },
        EditDateFormat,
        Dates.Patterns,
        Readings?.Characters,
        DateReadings?.Characters,
        [.. Dates.Eras.Select(static x => new EraInfo(x.Name, x.ShortName, x.Initial, x.Start.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), x.FirstYear))],
        Dates.FirstYear?.Text,
        Dates.FirstYear?.Unit);

    [GeneratedRegex("^(?=[^y]*y)(?=[^M]*M)(?=[^d]*d)[yMd/.-]+$")]
    private static partial Regex NumericDatePattern();

    [GeneratedRegex("y+")]
    private static partial Regex YearDigits();
}
