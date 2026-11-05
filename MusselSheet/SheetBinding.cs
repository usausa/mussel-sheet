namespace MusselSheet;

using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;

using MusselSheet.Internal;

public sealed class SheetBinding<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] T> : IDisposable, ISheetBinding
    where T : class
{
    private readonly BindingSync sync;

    private readonly List<CellLink> cells = [];

    private readonly List<IRowsBinding> lists = [];

    private bool disposed;

    internal SheetBinding(Sheet sheet, T source)
    {
        sync = new BindingSync(sheet);
        Source = source;
        if (source is INotifyPropertyChanged notifier)
        {
            notifier.PropertyChanged += OnSourceChanged;
        }
    }

    public Sheet Sheet => sync.Sheet;

    public T Source { get; }

    public SheetBinding<T> Cell<TValue>(string address, Expression<Func<T, TValue>> property)
    {
        var cell = CellAddress.Parse(address);
        return Cell(cell.Row, cell.Column, property);
    }

    public SheetBinding<T> Cell<TValue>(int row, int column, Expression<Func<T, TValue>> property)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        ArgumentOutOfRangeException.ThrowIfNegative(row);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(row, Sheet.RowCount);
        ArgumentOutOfRangeException.ThrowIfNegative(column);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(column, Sheet.ColumnCount);
        var link = new CellLink(Sheet.Rows[row], Sheet.Columns[column], BoundProperty.Of(property));
        cells.Add(link);
        sync.Show(Source, link.Property, row, column);
        return this;
    }

    public SheetBinding<T> Rows<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor | DynamicallyAccessedMemberTypes.PublicProperties)] TItem>(string range, Expression<Func<T, IList<TItem>>> list, Action<SheetRowsBinding<TItem>> columns)
        where TItem : class =>
        Rows(CellRange.Parse(range), list, columns);

    public SheetBinding<T> Rows<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor | DynamicallyAccessedMemberTypes.PublicProperties)] TItem>(CellRange range, Expression<Func<T, IList<TItem>>> list, Action<SheetRowsBinding<TItem>> columns)
        where TItem : class
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        ArgumentNullException.ThrowIfNull(columns);
        var rows = new SheetRowsBinding<TItem>(sync, Source, range, BoundProperty.Of(list));
        columns(rows);
        rows.Attach();
        lists.Add(rows);
        return this;
    }

    public void Refresh()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        ShowCells(null);
        foreach (var list in lists.ToArray())
        {
            list.OnSourceChanged(null, true);
        }
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        if (Source is INotifyPropertyChanged notifier)
        {
            notifier.PropertyChanged -= OnSourceChanged;
        }

        foreach (var list in lists)
        {
            list.Detach();
        }

        Sheet.Unbind(this);
    }

    void ISheetBinding.OnSheetChanged(SheetChangedEventArgs e)
    {
        if (disposed || sync.IsApplying)
        {
            return;
        }

        switch (e.Kind)
        {
            case SheetChangeKind.Values when e.Range is { } range:
                foreach (var link in cells.ToArray())
                {
                    if (IsCurrent(link) && range.Contains(link.Row.Index, link.Column.Index))
                    {
                        sync.Write(Source, link.Property, link.Row.Index, link.Column.Index);
                    }
                }

                foreach (var list in lists.ToArray())
                {
                    list.OnValues(range);
                }

                break;
            case SheetChangeKind.RowsInserted:
                foreach (var list in lists.ToArray())
                {
                    list.OnRowsInserted(e.Index, e.Count);
                }

                break;
            case SheetChangeKind.RowsDeleted:
                foreach (var list in lists.ToArray())
                {
                    list.OnRowsRemoved(e.Index, e.Count);
                }

                break;
        }
    }

    BoundProperty? ISheetBinding.FindProperty(int row, int column)
    {
        if (disposed)
        {
            return null;
        }

        foreach (var link in cells)
        {
            if (IsCurrent(link) && (link.Row.Index == row) && (link.Column.Index == column))
            {
                return link.Property;
            }
        }

        foreach (var list in lists)
        {
            if (list.FindProperty(row, column) is { } property)
            {
                return property;
            }
        }

        return null;
    }

    private void OnSourceChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (disposed)
        {
            return;
        }

        ShowCells(e.PropertyName);
        foreach (var list in lists.ToArray())
        {
            list.OnSourceChanged(e.PropertyName, false);
        }
    }

    private void ShowCells(string? name)
    {
        foreach (var link in cells.ToArray())
        {
            if (IsCurrent(link) && (string.IsNullOrEmpty(name) || (link.Property.Name == name)))
            {
                sync.Show(Source, link.Property, link.Row.Index, link.Column.Index);
            }
        }
    }

    // A cell whose row or column was deleted is left alone until the deletion is undone
    private bool IsCurrent(CellLink link) => Sheet.Contains(link.Row) && Sheet.Contains(link.Column);

    private sealed record CellLink(SheetRow Row, SheetColumn Column, BoundProperty Property);
}
