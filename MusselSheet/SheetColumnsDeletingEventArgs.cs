namespace MusselSheet;

public sealed class SheetColumnsDeletingEventArgs : SheetStructureChangingEventArgs
{
    internal SheetColumnsDeletingEventArgs(Sheet sheet, int index, int count)
        : base(sheet, index, count)
    {
    }
}
