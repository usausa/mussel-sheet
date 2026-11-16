namespace MusselSheet;

public sealed record ResolvedCellStyle
{
    public static ResolvedCellStyle Default { get; } = new();

    public HorizontalAlignment HorizontalAlignment { get; init; } = HorizontalAlignment.General;

    public VerticalAlignment VerticalAlignment { get; init; } = VerticalAlignment.Center;

    public bool Bold { get; init; }

    public bool Italic { get; init; }

    public bool Underline { get; init; }

    public bool Strikethrough { get; init; }

    public int? FontSize { get; init; }

    public int IndentLevel { get; init; }

    public string? FontColor { get; init; }

    public string? FillColor { get; init; }

    public string? CssClass { get; init; }

    public ResolvedCellBorders Borders { get; init; } = ResolvedCellBorders.None;
}
