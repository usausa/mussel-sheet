namespace MusselSheet;

public sealed record ResolvedCellBorders(CellBorder? Top, CellBorder? Right, CellBorder? Bottom, CellBorder? Left)
{
    public static ResolvedCellBorders None { get; } = new(null, null, null, null);
}
