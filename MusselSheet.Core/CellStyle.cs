namespace MusselSheet;

public sealed class CellStyle
{
    public HorizontalAlignment? HorizontalAlignment { get; set; }

    public VerticalAlignment? VerticalAlignment { get; set; }

    public bool? Bold { get; set; }

    public bool? Italic { get; set; }

    public bool? Underline { get; set; }

    public bool? Strikethrough { get; set; }

    public int? FontSize { get; set; }

    public int? IndentLevel { get; set; }

    public string? FontColor { get; set; }

    public string? FillColor { get; set; }

    public string? CssClass { get; set; }

    public CellBorders Borders
    {
        get => field ??= new CellBorders();
        set;
    }

    internal CellStyle Clone() => new()
    {
        HorizontalAlignment = HorizontalAlignment,
        VerticalAlignment = VerticalAlignment,
        Bold = Bold,
        Italic = Italic,
        Underline = Underline,
        Strikethrough = Strikethrough,
        FontSize = FontSize,
        IndentLevel = IndentLevel,
        FontColor = FontColor,
        FillColor = FillColor,
        CssClass = CssClass,
        Borders = Borders.Clone()
    };
}
