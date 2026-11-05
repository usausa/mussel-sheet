namespace MusselSheet;

public sealed class SheetPastingEventArgs : EventArgs
{
    public Sheet Sheet { get; }

    public CellRange Range { get; }

    public IReadOnlyList<IReadOnlyList<string>> Values { get; }

    public bool Cancel { get; set; }

    internal SheetPastingEventArgs(Sheet sheet, CellRange range, IReadOnlyList<IReadOnlyList<string>> values)
    {
        Sheet = sheet;
        Range = range;
        Values = values;
    }
}
