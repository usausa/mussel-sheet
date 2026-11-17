namespace MusselSheet;

using System.Globalization;

using MusselSheet.Internal;

public sealed class SheetRow
{
    private int? height;

    internal SheetRow(Sheet sheet, int columnCount, int? initialHeight)
    {
        Sheet = sheet;
        Cells = new CellData?[columnCount];
        height = initialHeight;
    }

    public Sheet Sheet { get; }

    public int Index { get; internal set; }

    // A row that left the sheet comes back only by undo or redo, and then belongs where it was before
    internal bool WasRemoved { get; set; }

    public int? Height
    {
        get => height;
        set
        {
            if (height != value)
            {
                height = value;
                Sheet.NotifyLayout();
            }
        }
    }

    public int ActualHeight => height ?? Sheet.DefaultRowHeight;

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

    public string HeaderText => Header ?? (Index + 1).ToString(CultureInfo.InvariantCulture);

    internal CellData?[] Cells { get; set; }

    internal int ContentVersion { get; set; }
}
