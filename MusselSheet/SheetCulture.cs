namespace MusselSheet;

// Each item is searched from the culture to its parents; an unset or empty item does not count
public sealed class SheetCulture
{
    public SheetTexts? Texts { get; set; }

    public string? DateFormat { get; set; }

    public string? EditDateFormat { get; set; }

    public DayOfWeek? FirstDayOfWeek { get; set; }

    public IList<SheetEra> Eras { get; } = [];

    public SheetEraFirstYear? EraFirstYear { get; set; }

    public IList<string> DateForms { get; } = [];

    public SheetCharacterMap? Readings { get; set; }

    public SheetCharacterMap? DateReadings { get; set; }
}
