namespace MusselSheet.Internal;

using System.Reflection;

internal interface ISheetBinding
{
    bool IsApplying { get; }

    void OnSheetChanged(SheetChangedEventArgs e, ValueCells? targets);

    BoundProperty? FindProperty(int row, int column);

    bool HoldsRow(SheetRow row);

    bool HoldsReadOnlyItem(SheetRow row);

    bool HasEmptyListAt(int index);
}

internal interface IRowsBinding
{
    void OnValues(ValueCells cells);

    void OnRowsInserted(int index, int count);

    void OnRowsRemoved(int index, int count, IReadOnlyList<SheetRow> removed);

    void OnColumnsInserted(int index, int count);

    void OnSourceChanged(string? name, bool reload);

    BoundProperty? FindProperty(int row, int column);

    CellRange Frame { get; }

    bool Holds(SheetRow row);

    bool HoldsReadOnlyItem(SheetRow row);

    bool IsEmptyAt(int index);

    void Detach();
}

internal sealed class ValueCells(SheetRow[] rows, SheetColumn[] columns)
{
    public SheetRow[] Rows { get; } = rows;

    public bool Contains(SheetColumn column) => Array.IndexOf(columns, column) >= 0;

    public bool Contains(SheetRow row, SheetColumn column) => (Array.IndexOf(Rows, row) >= 0) && Contains(column);

    public CellRange? CurrentRange(Sheet sheet)
    {
        var top = Int32.MaxValue;
        var bottom = -1;
        foreach (var row in Rows)
        {
            if (sheet.Contains(row))
            {
                top = Math.Min(top, row.Index);
                bottom = Math.Max(bottom, row.Index);
            }
        }

        var left = Int32.MaxValue;
        var right = -1;
        foreach (var column in columns)
        {
            if (sheet.Contains(column))
            {
                left = Math.Min(left, column.Index);
                right = Math.Max(right, column.Index);
            }
        }

        return (bottom < 0) || (right < 0) ? null : new CellRange(top, left, bottom, right);
    }
}

internal sealed class BindingSync
{
    private int applying;

    public BindingSync(Sheet sheet)
    {
        Sheet = sheet;
    }

    public Sheet Sheet { get; }

    // True while the binding itself changes the sheet, so it can ignore the resulting change notifications
    public bool IsApplying => applying > 0;

    public void Apply(Action action)
    {
        applying++;
        try
        {
            action();
        }
        finally
        {
            applying--;
        }
    }

    // Skips values the property type cannot take; a setter exception becomes the error mark; true when the cell is to show the property back
    public bool Write(object target, BoundProperty property, SheetRow row, SheetColumn column)
    {
        if (!property.CanWrite || !IsCurrent(row, column) || !property.TryConvert(Sheet.GetValue(row.Index, column.Index), out var value))
        {
            return false;
        }

        string? error = null;
        if (!Equals(property.Get(target), value))
        {
            try
            {
                property.Set(target, value);
            }
            catch (TargetInvocationException ex) when (ex.InnerException is not null)
            {
                error = ex.InnerException.Message;
            }
        }

        // The setter can insert or delete rows and columns, so the cell is found again
        if (!IsCurrent(row, column))
        {
            return false;
        }

        if (error is not null)
        {
            Sheet.SetError(row.Index, column.Index, error);
            return false;
        }

        return true;
    }

    // Does nothing when the value is the same, which keeps the typed text and the error mark; a covered cell holds no value
    public void Show(object target, BoundProperty property, int row, int column)
    {
        var value = property.Get(target);
        if (!Sheet.IsCovered(row, column) && !property.IsSame(Sheet.GetValue(row, column), value))
        {
            Apply(() => Sheet.SetValue(row, column, value));
        }
    }

    private bool IsCurrent(SheetRow row, SheetColumn column) => Sheet.Contains(row) && Sheet.Contains(column);

    public void Clear(int row, int column)
    {
        if (Sheet.GetValue(row, column) is not null)
        {
            Apply(() => Sheet.SetValue(row, column, null));
        }
    }
}
