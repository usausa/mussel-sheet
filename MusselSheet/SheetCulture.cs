namespace MusselSheet;

public sealed class SheetCulture
{
    public SheetTexts? Texts { get; set; }

    public string? DateFormat { get; set; }

    public DayOfWeek? FirstDayOfWeek { get; set; }

    public bool? JapaneseDates { get; set; }
}
