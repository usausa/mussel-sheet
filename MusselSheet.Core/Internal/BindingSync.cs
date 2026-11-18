namespace MusselSheet.Internal;

using System.Reflection;

internal interface ISheetBinding
{
    void OnSheetChanged(SheetChangedEventArgs e);

    BoundProperty? FindProperty(int row, int column);

    bool HoldsRow(SheetRow row);

    bool HasEmptyListAt(int index);
}

internal interface IRowsBinding
{
    void OnValues(CellRange range);

    void OnRowsInserted(int index, int count);

    void OnRowsRemoved(int index, int count);

    void OnColumnsInserted(int index, int count);

    void OnSourceChanged(string? name, bool reload);

    BoundProperty? FindProperty(int row, int column);

    CellRange Frame { get; }

    bool Holds(SheetRow row);

    bool IsEmptyAt(int index);

    void Detach();
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
        if (!property.CanWrite || !Holds(row, column) || !property.TryConvert(Sheet.GetValue(row.Index, column.Index), out var value))
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
        if (!Holds(row, column))
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

    private bool Holds(SheetRow row, SheetColumn column) => Sheet.Contains(row) && Sheet.Contains(column);

    public void Clear(int row, int column)
    {
        if (Sheet.GetValue(row, column) is not null)
        {
            Apply(() => Sheet.SetValue(row, column, null));
        }
    }
}
