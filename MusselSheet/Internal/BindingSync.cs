namespace MusselSheet.Internal;

using System.Reflection;

internal interface ISheetBinding
{
    void OnSheetChanged(SheetChangedEventArgs e);

    BoundProperty? FindProperty(int row, int column);
}

internal interface IRowsBinding
{
    void OnValues(CellRange range);

    void OnRowsInserted(int index, int count);

    void OnRowsRemoved(int index, int count);

    void OnSourceChanged(string? name, bool reload);

    BoundProperty? FindProperty(int row, int column);

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

    // Skips values that cannot become the property type (set by the app, or accepted with an error mark).
    // A setter exception becomes the cell's error mark. The property value is shown back afterwards,
    // because the setter may have normalized it or an empty cell may have produced a default.
    public void Write(object target, BoundProperty property, int row, int column)
    {
        if (!property.CanWrite || !property.TryConvert(Sheet.GetValue(row, column), out var value))
        {
            return;
        }

        if (!Equals(property.Get(target), value))
        {
            try
            {
                property.Set(target, value);
            }
            catch (TargetInvocationException ex) when (ex.InnerException is not null)
            {
                Sheet.SetError(row, column, ex.InnerException.Message);
                return;
            }
        }

        Show(target, property, row, column);
    }

    // Does nothing when the value is the same, which keeps the typed text and the error mark
    public void Show(object target, BoundProperty property, int row, int column)
    {
        var value = property.Get(target);
        if (!property.IsSame(Sheet.GetValue(row, column), value))
        {
            Apply(() => Sheet.SetValue(row, column, value));
        }
    }

    public void Clear(int row, int column)
    {
        if (Sheet.GetValue(row, column) is not null)
        {
            Apply(() => Sheet.SetValue(row, column, null));
        }
    }
}
