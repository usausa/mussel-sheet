namespace MusselSheet;

public sealed class SheetChangedEventArgs : EventArgs
{
    public SheetChangedEventArgs(SheetChangeKind kind, CellRange? range = null, int index = 0, int count = 0)
    {
        Kind = kind;
        Range = range;
        Index = index;
        Count = count;
    }

    public SheetChangeKind Kind { get; }

    public CellRange? Range { get; }

    public int Index { get; }

    public int Count { get; }
}
