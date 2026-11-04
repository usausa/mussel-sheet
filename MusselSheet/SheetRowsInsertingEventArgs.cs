namespace MusselSheet;

public sealed class SheetRowsInsertingEventArgs : SheetStructureChangingEventArgs
{
    internal SheetRowsInsertingEventArgs(Sheet sheet, int index, int count)
        : base(sheet, index, count)
    {
    }
}
