namespace MusselSheet;

public sealed class CellStyleDesign
{
    public string? HorizontalAlignment { get; set; }

    public string? VerticalAlignment { get; set; }

    public bool? Bold { get; set; }

    public bool? Italic { get; set; }

    public bool? Underline { get; set; }

    public bool? Strikethrough { get; set; }

    public int? FontSize { get; set; }

    public int? IndentLevel { get; set; }

    public string? FontColor { get; set; }

    public string? FillColor { get; set; }

    public string? CssClass { get; set; }

    public CellBorderDesign? Top { get; set; }

    public CellBorderDesign? Right { get; set; }

    public CellBorderDesign? Bottom { get; set; }

    public CellBorderDesign? Left { get; set; }
}
