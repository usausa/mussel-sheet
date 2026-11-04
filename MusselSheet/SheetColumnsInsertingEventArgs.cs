namespace MusselSheet;

public sealed class SheetColumnsInsertingEventArgs : SheetStructureChangingEventArgs
{
    internal SheetColumnsInsertingEventArgs(Sheet sheet, int index, int count)
        : base(sheet, index, count)
    {
    }
}
