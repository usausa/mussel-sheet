namespace MusselSheet;

public readonly record struct CellBorder(BorderLineStyle Style, string? Color = null)
{
    public static CellBorder None => new(BorderLineStyle.None);

    public static CellBorder Thin => new(BorderLineStyle.Thin);

    public static CellBorder Medium => new(BorderLineStyle.Medium);

    public static CellBorder Thick => new(BorderLineStyle.Thick);

    public static CellBorder Dashed => new(BorderLineStyle.Dashed);

    public static CellBorder Dotted => new(BorderLineStyle.Dotted);

    public static CellBorder DoubleLine => new(BorderLineStyle.DoubleLine);

    public CellBorder WithColor(string color) => this with { Color = color };
}
