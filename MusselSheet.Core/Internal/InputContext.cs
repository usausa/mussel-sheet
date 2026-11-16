namespace MusselSheet.Internal;

using System.Globalization;

internal sealed record InputContext(CultureInfo Culture, DateOnly Today, SheetTexts Texts, string DateFormat, ReadingRules Reading)
{
    public static string Fill(string template, string first, string? second = null)
    {
        var text = template.Replace("{0}", first, StringComparison.Ordinal);
        return second is null ? text : text.Replace("{1}", second, StringComparison.Ordinal);
    }

    public static string Fill(string template, int value) => Fill(template, value.ToString(CultureInfo.InvariantCulture));

    public DateRules Dates => Reading.Dates;

    public string DateMessage => Fill(Texts.Date, DateRules.IsEraFormat(DateFormat) ? Dates.Format(Today, DateFormat, Culture) : DateFormat);
}
