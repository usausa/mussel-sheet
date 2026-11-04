namespace MusselSheet;

public sealed class SheetRowsDeletingEventArgs : SheetStructureChangingEventArgs
{
    internal SheetRowsDeletingEventArgs(Sheet sheet, int index, int count)
        : base(sheet, index, count)
    {
    }
}
