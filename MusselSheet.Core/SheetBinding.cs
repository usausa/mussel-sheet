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
        var link = new CellLink(Sheet.Rows[row], Sheet.Columns[column], BoundProperty.Of(property));
        cells.Add(link);
        sync.Show(Source, link.Property, row, column);
        Sheet.OnRebound();
        return this;
    }

    public SheetBinding<T> Rows<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor | DynamicallyAccessedMemberTypes.PublicProperties)] TItem>(string range, Expression<Func<T, IList<TItem>>> list, Action<SheetRowsBinding<TItem>> columns)
        where TItem : class =>
        Rows(CellRange.Parse(range), list, columns);

    public SheetBinding<T> Rows<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor | DynamicallyAccessedMemberTypes.PublicProperties)] TItem>(CellRange range, Expression<Func<T, IList<TItem>>> list, Action<SheetRowsBinding<TItem>> columns)
        where TItem : class
    {
        ObjectDisposedException.ThrowIf(disposed, this);
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

    // While one list adds or removes rows for its own items, the other lists of this binding only follow those rows
    private void FollowRowsOfAnotherList(SheetChangedEventArgs e)
    {
        if (e.Kind is not (SheetChangeKind.RowsInserted or SheetChangeKind.RowsDeleted))
        {
            return;
        }

        foreach (var list in lists.ToArray())
        {
            if (ReferenceEquals(list, Sheet.ChangingList))
            {
                continue;
            }

            if (e.Kind == SheetChangeKind.RowsInserted)
            {
                list.OnRowsInserted(e.Index, e.Count);
            }
            else if (e.Kind == SheetChangeKind.RowsDeleted)
            {
                list.OnRowsRemoved(e.Index, e.Count, e.RemovedRows);
            }
        }
    }

    bool ISheetBinding.IsApplying => sync.IsApplying;

    void ISheetBinding.OnSheetChanged(SheetChangedEventArgs e, ValueCells? targets)
    {
        if (disposed)
        {
            return;
        }

        if (sync.IsApplying)
        {
            FollowRowsOfAnotherList(e);
            return;
        }

        switch (e.Kind)
        {
            case SheetChangeKind.Values when targets is not null:
                foreach (var link in cells.ToArray())
                {
                    if (IsCurrent(link) && targets.Contains(link.Row, link.Column) && sync.Write(Source, link.Property, link.Row, link.Column))
                    {
                        sync.Show(Source, link.Property, link.Row.Index, link.Column.Index);
                    }
                }

                foreach (var list in lists.ToArray())
                {
                    list.OnValues(targets);
                }

                break;
            case SheetChangeKind.RowsInserted:
                ShowRestored(e.Index, e.Count, true);
                foreach (var list in lists.ToArray())
                {
                    list.OnRowsInserted(e.Index, e.Count);
                }

                break;
            case SheetChangeKind.ColumnsInserted:
                ShowRestored(e.Index, e.Count, false);
                foreach (var list in lists.ToArray())
                {
                    list.OnColumnsInserted(e.Index, e.Count);
                }

                break;
            case SheetChangeKind.RowsDeleted:
                foreach (var list in lists.ToArray())
                {
                    list.OnRowsRemoved(e.Index, e.Count, e.RemovedRows);
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

    bool ISheetBinding.HoldsRow(SheetRow row) => !disposed && lists.Exists(x => x.Holds(row));

    bool ISheetBinding.HoldsReadOnlyItem(SheetRow row) => !disposed && lists.Exists(x => x.HoldsReadOnlyItem(row));

    bool ISheetBinding.HasEmptyListAt(int index) => !disposed && lists.Exists(x => x.IsEmptyAt(index));

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
            if (IsCurrent(link) && (String.IsNullOrEmpty(name) || (link.Property.Name == name)))
            {
                sync.Show(Source, link.Property, link.Row.Index, link.Column.Index);
            }
        }
    }

    // Undo puts deleted rows and columns back with their old values, so the cells show the source again
    private void ShowRestored(int index, int count, bool rows)
    {
        foreach (var link in cells.ToArray())
        {
            var position = rows ? link.Row.Index : link.Column.Index;
            if (IsCurrent(link) && (position >= index) && (position < index + count))
            {
                sync.Show(Source, link.Property, link.Row.Index, link.Column.Index);
            }
        }
    }

    // A cell whose row or column was deleted is left alone until the deletion is undone
    private bool IsCurrent(CellLink link) => Sheet.Contains(link.Row) && Sheet.Contains(link.Column);

    private sealed record CellLink(SheetRow Row, SheetColumn Column, BoundProperty Property);
}
