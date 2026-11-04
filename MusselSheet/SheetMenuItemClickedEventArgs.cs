namespace MusselSheet;

public sealed class SheetMenuItemClickedEventArgs : EventArgs
{
    internal SheetMenuItemClickedEventArgs(Sheet sheet, string name, CellRange selection, CellAddress activeCell)
    {
        Sheet = sheet;
        Name = name;
        Selection = selection;
        ActiveCell = activeCell;
    }

    public Sheet Sheet { get; }

    public string Name { get; }

    public CellRange Selection { get; }

    public CellAddress ActiveCell { get; }
}
