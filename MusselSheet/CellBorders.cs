namespace MusselSheet;

public sealed class CellBorders
{
    public CellBorder? Top { get; set; }

    public CellBorder? Right { get; set; }

    public CellBorder? Bottom { get; set; }

    public CellBorder? Left { get; set; }

    public static CellBorders All(CellBorder line) => new() { Top = line, Right = line, Bottom = line, Left = line };

    internal CellBorders Clone() => new() { Top = Top, Right = Right, Bottom = Bottom, Left = Left };
}
