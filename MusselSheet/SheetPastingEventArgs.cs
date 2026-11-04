namespace MusselSheet;

public sealed class SheetPastingEventArgs : EventArgs
{
    internal SheetPastingEventArgs(Sheet sheet, CellRange range, IReadOnlyList<IReadOnlyList<string>> values)
    {
        Sheet = sheet;
        Range = range;
        Values = values;
    }

    public Sheet Sheet { get; }

    public CellRange Range { get; }

    public IReadOnlyList<IReadOnlyList<string>> Values { get; }

    public bool Cancel { get; set; }
}
