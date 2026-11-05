namespace MusselSheet;

public sealed class SheetFillingEventArgs : EventArgs
{
    public Sheet Sheet { get; }

    public CellRange Source { get; }

    public CellRange Target { get; }

    public bool Cancel { get; set; }

    internal SheetFillingEventArgs(Sheet sheet, CellRange source, CellRange target)
    {
        Sheet = sheet;
        Source = source;
        Target = target;
    }
}
