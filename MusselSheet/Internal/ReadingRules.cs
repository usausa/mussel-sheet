namespace MusselSheet.Internal;

using System.Globalization;
using System.Text;
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

    public string DateFormat { get; init; } = string.Empty;

    // A culture whose two number separators are the same has no grouping that could be told from the decimal point
    public static ReadingRules From(CultureInfo culture)
    {
        var numbers = culture.NumberFormat;
        var group = numbers.NumberGroupSeparator == numbers.NumberDecimalSeparator ? string.Empty : numbers.NumberGroupSeparator;
        return new(numbers.NumberDecimalSeparator, group, culture.DateTimeFormat.TimeSeparator, OrderOf(culture), LiteralSeparators(EditFormat(culture)))
        {
            DateFormat = culture.DateTimeFormat.ShortDatePattern
        };
    }

    // The edit text is read back, so its '/' stays '/' even where the culture's date separator is something else
    public static string LiteralSeparators(string format)
    {
        if (!format.Contains('/', StringComparison.Ordinal))
        {
            return format;
        }

        var builder = new StringBuilder(format.Length + 8);
        var quote = '\0';
        for (var i = 0; i < format.Length; i++)
        {
            var c = format[i];
            if (quote != '\0')
            {
                quote = c == quote ? '\0' : quote;
                builder.Append(c);
            }
            else if (c is '\'' or '"')
            {
                quote = c;
                builder.Append(c);
            }
            else if ((c == '\\') && (i + 1 < format.Length))
            {
                builder.Append(c).Append(format[++i]);
            }
            else if (c == '/')
            {
                builder.Append("'/'");
            }
            else
            {
                builder.Append(c);
            }
        }

        return builder.ToString();
    }

    // A value is always a Gregorian date, whatever calendar the culture has by default
    public static CultureInfo Gregorian(CultureInfo culture)
    {
        if (culture.DateTimeFormat.Calendar is GregorianCalendar)
        {
            return CultureInfo.ReadOnly(culture);
        }

        var own = culture.DateTimeFormat;
        var reference = (CultureInfo)CultureInfo.GetCultureInfo(culture.Name).Clone();
        reference.DateTimeFormat.Calendar = own.Calendar;
        var standard = reference.DateTimeFormat;
        var clone = (CultureInfo)culture.Clone();
        var format = clone.DateTimeFormat;
        format.Calendar = new GregorianCalendar();
        format.ShortDatePattern = Kept(own.ShortDatePattern, standard.ShortDatePattern, format.ShortDatePattern);
        format.LongDatePattern = Kept(own.LongDatePattern, standard.LongDatePattern, format.LongDatePattern);
        format.YearMonthPattern = Kept(own.YearMonthPattern, standard.YearMonthPattern, format.YearMonthPattern);
        format.MonthDayPattern = Kept(own.MonthDayPattern, standard.MonthDayPattern, format.MonthDayPattern);
        format.DateSeparator = Kept(own.DateSeparator, standard.DateSeparator, format.DateSeparator);
        format.DayNames = Kept(own.DayNames, standard.DayNames, format.DayNames);
        format.AbbreviatedDayNames = Kept(own.AbbreviatedDayNames, standard.AbbreviatedDayNames, format.AbbreviatedDayNames);
        format.ShortestDayNames = Kept(own.ShortestDayNames, standard.ShortestDayNames, format.ShortestDayNames);
        format.MonthNames = Kept(own.MonthNames, standard.MonthNames, format.MonthNames);
        format.AbbreviatedMonthNames = Kept(own.AbbreviatedMonthNames, standard.AbbreviatedMonthNames, format.AbbreviatedMonthNames);
        format.MonthGenitiveNames = Kept(own.MonthGenitiveNames, standard.MonthGenitiveNames, format.MonthGenitiveNames);
        format.AbbreviatedMonthGenitiveNames = Kept(own.AbbreviatedMonthGenitiveNames, standard.AbbreviatedMonthGenitiveNames, format.AbbreviatedMonthGenitiveNames);
        return CultureInfo.ReadOnly(clone);
    }

    private static string Kept(string own, string standard, string pinned) =>
        string.Equals(own, standard, StringComparison.Ordinal) ? pinned : own;

    private static string[] Kept(string[] own, string[] standard, string[] pinned) =>
        own.AsSpan().SequenceEqual(standard) ? pinned : own;

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
