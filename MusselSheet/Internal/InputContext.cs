namespace MusselSheet.Internal;

using System.Globalization;

internal sealed record InputContext(CultureInfo Culture, DateOnly Today, SheetTexts Texts, string DateFormat, ReadingRules Reading)
{
    public IReadOnlyList<SheetJapaneseEra> Eras { get; init; } = SheetJapaneseEra.BuiltIn;

    public static string Fill(string template, string first, string? second = null)
    {
        var text = template.Replace("{0}", first, StringComparison.Ordinal);
        return second is null ? text : text.Replace("{1}", second, StringComparison.Ordinal);
    }

    public static string Fill(string template, int value) => Fill(template, value.ToString(CultureInfo.InvariantCulture));

    public string DateMessage => Fill(Texts.Date, DateFormat);

    public IReadOnlyDictionary<string, string> ForScript() => new Dictionary<string, string>
    {
        ["required"] = Texts.Required,
        ["number"] = Texts.Number,
        ["date"] = DateMessage,
        ["time"] = Texts.Time,
        ["check"] = Texts.Check,
        ["list"] = Texts.List,
        ["between"] = Texts.Between,
        ["atLeast"] = Texts.AtLeast,
        ["atMost"] = Texts.AtMost,
        ["maxLength"] = Texts.MaxLength,
        ["maxByteLength"] = Texts.MaxByteLength,
        ["pattern"] = Texts.Pattern,
        ["mask"] = Texts.Mask,
        ["fullwidth"] = Texts.Fullwidth,
        ["halfwidth"] = Texts.Halfwidth,
        ["hiragana"] = Texts.Hiragana,
        ["katakana"] = Texts.Katakana,
        ["halfwidthKatakana"] = Texts.HalfwidthKatakana,
        ["alphanumeric"] = Texts.Alphanumeric,
        ["digits"] = Texts.Digits,
        ["notEditable"] = Texts.NotEditable,
        ["canceled"] = Texts.Canceled,
        ["menuCut"] = Texts.MenuCut,
        ["menuCopy"] = Texts.MenuCopy,
        ["menuPaste"] = Texts.MenuPaste,
        ["menuInsertRowsAbove"] = Texts.MenuInsertRowsAbove,
        ["menuInsertRowsBelow"] = Texts.MenuInsertRowsBelow,
        ["menuDeleteRows"] = Texts.MenuDeleteRows,
        ["menuInsertColumnsLeft"] = Texts.MenuInsertColumnsLeft,
        ["menuInsertColumnsRight"] = Texts.MenuInsertColumnsRight,
        ["menuDeleteColumns"] = Texts.MenuDeleteColumns,
        ["menuPasteFailed"] = Texts.MenuPasteFailed,
        ["calendarToday"] = Texts.CalendarToday,
        ["calendarPrevious"] = Texts.CalendarPrevious,
        ["calendarNext"] = Texts.CalendarNext,
        ["cellInput"] = Texts.CellInput,
    };
}
