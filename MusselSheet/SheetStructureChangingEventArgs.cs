namespace MusselSheet;

public abstract class SheetStructureChangingEventArgs : EventArgs
{
    public Sheet Sheet { get; }

    public int Index { get; }

    public int Count { get; }

    public bool Cancel { get; set; }

    private protected SheetStructureChangingEventArgs(Sheet sheet, int index, int count)
    {
        Sheet = sheet;
        Index = index;
        Count = count;
    }
}
