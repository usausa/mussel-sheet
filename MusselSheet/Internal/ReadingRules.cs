namespace MusselSheet.Internal;

using System.Globalization;
using System.Text.RegularExpressions;

internal enum DateOrder
{
    YearMonthDay,
    MonthDayYear,
    DayMonthYear,
}

// Separators and date order come from the sheet's CultureInfo (pass a custom one to Sheet.Culture to
// change them), so display, edit text and reading cannot diverge. The JS validation receives the same values.
internal sealed partial record ReadingRules(string DecimalSeparator, string GroupSeparator, DateOrder DateOrder, string EditDateFormat, bool Japanese)
{
    public static ReadingRules From(CultureInfo culture, bool japanese)
    {
        var numbers = culture.NumberFormat;
        return new(numbers.NumberDecimalSeparator, numbers.NumberGroupSeparator, OrderOf(culture), EditFormat(culture), japanese);
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
            _ => "yyyy/MM/dd",
        };
    }

    public ReadingInfo ForScript() => new(
        DecimalSeparator,
        GroupSeparator,
        DateOrder switch
        {
            DateOrder.DayMonthYear => "dmy",
            DateOrder.MonthDayYear => "mdy",
            _ => "ymd",
        },
        EditDateFormat,
        Japanese);

    [GeneratedRegex("^(?=[^y]*y)(?=[^M]*M)(?=[^d]*d)[yMd/.-]+$")]
    private static partial Regex NumericDatePattern();

    [GeneratedRegex("y+")]
    private static partial Regex YearDigits();
}
