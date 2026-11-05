namespace MusselSheet;

public sealed class SheetColumn
{
    private int? width;

    internal SheetColumn(Sheet sheet, int? initialWidth)
    {
        Sheet = sheet;
        width = initialWidth;
    }

    public Sheet Sheet { get; }

    public int Index { get; internal set; }

    public int? Width
    {
        get => width;
        set
        {
            if (width != value)
            {
                width = value;
                Sheet.NotifyLayout();
            }
        }
    }

    public int ActualWidth => width ?? Sheet.DefaultColumnWidth;

    public string? Header
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                Sheet.NotifyLayout();
            }
        }
    }

    public string HeaderText => Header ?? CellAddress.ColumnName(Index);
}
