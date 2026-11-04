namespace MusselSheet;

public sealed class SheetFillingEventArgs : EventArgs
{
    internal SheetFillingEventArgs(Sheet sheet, CellRange source, CellRange target)
    {
        Sheet = sheet;
        Source = source;
        Target = target;
    }

    public Sheet Sheet { get; }

    public CellRange Source { get; }

    public CellRange Target { get; }

    public bool Cancel { get; set; }
}
