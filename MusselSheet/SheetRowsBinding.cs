namespace MusselSheet;

using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;

using MusselSheet.Internal;

public sealed class SheetRowsBinding<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor | DynamicallyAccessedMemberTypes.PublicProperties)] TItem> : IRowsBinding
    where TItem : class
{
    private readonly BindingSync sync;

    private readonly object source;

    private readonly BoundProperty listProperty;

    private readonly CellRange frame;

    private readonly List<ColumnLink> columns = [];

    private readonly List<SheetRow> rows = [];

    private readonly Dictionary<SheetRow, TItem> items = [];

    // The element shown in each row, so undoing a row deletion brings back the same element.
    //  Weak, so entries go away when the row object leaves the undo history.
    private readonly ConditionalWeakTable<SheetRow, TItem> remembered = [];

    private IList<TItem>? list;

    // Row count of the detail frame at bind time; kept even when the app removes elements
    private int minimum;

    // Where the detail region sits once the user has deleted all of its rows
    private int emptyTop;

    private int changingList;

    private bool attached;

    internal SheetRowsBinding(BindingSync sync, object source, CellRange frame, BoundProperty listProperty)
    {
        if (frame.IsEntireColumns || (frame.Bottom >= sync.Sheet.RowCount))
        {
            throw new ArgumentOutOfRangeException(nameof(frame), "The range of the rows must be inside the sheet.");
        }

        this.sync = sync;
        this.source = source;
        this.frame = frame;
        this.listProperty = listProperty;
    }

    public Func<TItem>? Create { get; set; }

    public SheetRowsBinding<TItem> Column<TValue>(string column, Expression<Func<TItem, TValue>> property)
    {
        if (!CellAddress.TryParseColumn(column, out var index))
        {
            throw new ArgumentException($"'{column}' is not a column name.", nameof(column));
        }

        return Column(index, property);
    }

    public SheetRowsBinding<TItem> Column<TValue>(int column, Expression<Func<TItem, TValue>> property)
    {
        if (!frame.IsEntireRows && ((column < frame.Left) || (column > frame.Right)))
        {
            throw new ArgumentOutOfRangeException(nameof(column), "The column must be inside the range of the rows.");
        }

        var link = new ColumnLink(sync.Sheet.Columns[column], BoundProperty.Of(property));
        columns.Add(link);
        if (attached)
        {
            sync.Apply(() =>
            {
                foreach (var row in rows)
                {
                    ShowCell(row, link);
                }
            });
        }

        return this;
    }

    internal void Attach()
    {
        if ((Create is null) && (typeof(TItem).GetConstructor(Type.EmptyTypes) is null))
        {
            throw new InvalidOperationException($"{typeof(TItem).Name} needs a parameterless constructor, or set Create to make new items.");
        }

        for (var r = frame.Top; r <= frame.Bottom; r++)
        {
            rows.Add(sync.Sheet.Rows[r]);
        }

        minimum = rows.Count;
        emptyTop = frame.Top;
        attached = true;
        Load();
    }

    void IRowsBinding.Detach() => DetachList();

    void IRowsBinding.OnValues(CellRange range)
    {
        foreach (var row in rows.ToArray())
        {
            var r = row.Index;
            if ((r < range.Top) || (r > range.Bottom) || !sync.Sheet.Contains(row))
            {
                continue;
            }

            if (items.TryGetValue(row, out var item))
            {
                foreach (var link in columns)
                {
                    if (IsCurrent(link) && (link.Column.Index >= range.Left) && (link.Column.Index <= range.Right))
                    {
                        sync.Write(item, link.Property, r, link.Column.Index);
                    }
                }
            }
            else if (columns.Exists(x => IsCurrent(x) && (sync.Sheet.GetValue(r, x.Column.Index) is not null)))
            {
                AddItemFor(row);
            }
        }
    }

    // Rows inserted directly below a detail row (also below the last) or directly above the first join the detail as empty rows.
    void IRowsBinding.OnRowsInserted(int index, int count)
    {
        var sheet = sync.Sheet;
        var position = InsertPosition(index, count);
        if (position < 0)
        {
            if ((rows.Count == 0) && (index <= emptyTop))
            {
                emptyTop += count;
            }

            return;
        }

        var inserted = new List<SheetRow>(count);
        for (var r = index; r < index + count; r++)
        {
            inserted.Add(sheet.Rows[r]);
        }

        rows.InsertRange(position, inserted);
        foreach (var row in inserted)
        {
            if (remembered.TryGetValue(row, out var item) && (list is { IsReadOnly: false }))
            {
                ChangeList(x => x.Insert(PositionOf(row), item));
                Bind(row, item);
            }
        }
    }

    void IRowsBinding.OnRowsRemoved(int index, int count)
    {
        var sheet = sync.Sheet;
        if (!rows.Exists(x => !sheet.Contains(x)))
        {
            if ((rows.Count == 0) && (index < emptyTop))
            {
                emptyTop = Math.Max(index, emptyTop - count);
            }

            return;
        }

        var positions = new List<int>();
        var position = 0;
        foreach (var row in rows)
        {
            if (items.ContainsKey(row))
            {
                if (!sheet.Contains(row))
                {
                    positions.Add(position);
                }

                position++;
            }
        }

        if (list is { IsReadOnly: false })
        {
            ChangeList(x =>
            {
                for (var i = positions.Count - 1; i >= 0; i--)
                {
                    x.RemoveAt(positions[i]);
                }
            });
        }

        foreach (var row in rows.Where(x => !sheet.Contains(x)).ToArray())
        {
            if (items.Remove(row, out var item))
            {
                Unsubscribe(item);
            }
        }

        rows.RemoveAll(x => !sheet.Contains(x));
        if (rows.Count == 0)
        {
            emptyTop = index;
        }
    }

    BoundProperty? IRowsBinding.FindProperty(int row, int column)
    {
        var sheet = sync.Sheet;
        if ((row < 0) || (row >= sheet.RowCount) || !rows.Contains(sheet.Rows[row]))
        {
            return null;
        }

        foreach (var link in columns)
        {
            if (IsCurrent(link) && (link.Column.Index == column))
            {
                return link.Property;
            }
        }

        return null;
    }

    void IRowsBinding.OnSourceChanged(string? name, bool reload)
    {
        if (reload || ((string.IsNullOrEmpty(name) || (name == listProperty.Name)) && !ReferenceEquals(listProperty.Get(source), list)))
        {
            Load();
        }
    }

    private void Load()
    {
        DetachList();
        list = listProperty.Get(source) as IList<TItem>;
        if (list is INotifyCollectionChanged notifier)
        {
            notifier.CollectionChanged += OnListChanged;
        }

        var current = list;
        var count = current?.Count ?? 0;
        sync.Apply(() =>
        {
            using (sync.Sheet.BeginUpdate())
            {
                var target = Math.Max(minimum, count);
                if (rows.Count < target)
                {
                    InsertRowsAt(rows.Count, target - rows.Count);
                }
                else if (rows.Count > target)
                {
                    RemoveRowsAt(target, rows.Count - target);
                }

                for (var i = 0; i < rows.Count; i++)
                {
                    if (i < count)
                    {
                        Bind(rows[i], current![i]);
                    }
                    else
                    {
                        ClearRow(rows[i]);
                    }
                }
            }
        });
    }

    private void OnListChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if ((changingList > 0) || (list is null))
        {
            return;
        }

        sync.Apply(() =>
        {
            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add when (e.NewItems is { } added) && (e.NewStartingIndex >= 0):
                    for (var i = 0; i < added.Count; i++)
                    {
                        AddedAt(e.NewStartingIndex + i, (TItem)added[i]!);
                    }

                    break;
                case NotifyCollectionChangedAction.Remove when (e.OldItems is { } removed) && (e.OldStartingIndex >= 0):
                    for (var i = 0; i < removed.Count; i++)
                    {
                        RemovedAt(e.OldStartingIndex);
                    }

                    break;
                case NotifyCollectionChangedAction.Replace when (e.NewItems is { } replaced) && (e.NewStartingIndex >= 0):
                    for (var i = 0; i < replaced.Count; i++)
                    {
                        ReplacedAt(e.NewStartingIndex + i, (TItem)replaced[i]!);
                    }

                    break;
                default:
                    Load();
                    break;
            }
        });
    }

    // Shown in the next empty row after the previous element's row; a row is inserted if there is none
    private void AddedAt(int position, TItem item)
    {
        var elementRows = ElementRows();
        if (position > elementRows.Count)
        {
            Load();
            return;
        }

        var at = position == 0 ? 0 : rows.IndexOf(elementRows[position - 1]) + 1;
        if ((at >= rows.Count) || items.ContainsKey(rows[at]))
        {
            InsertRowsAt(at, 1);
        }

        Bind(rows[at], item);
    }

    private void RemovedAt(int position)
    {
        var elementRows = ElementRows();
        if (position >= elementRows.Count)
        {
            Load();
            return;
        }

        var row = elementRows[position];
        if (items.Remove(row, out var item))
        {
            Unsubscribe(item);
        }

        if (rows.Count <= minimum)
        {
            InsertRowsAt(rows.Count, 1);
        }

        RemoveRowsAt(rows.IndexOf(row), 1);
    }

    private void ReplacedAt(int position, TItem item)
    {
        var elementRows = ElementRows();
        if (position >= elementRows.Count)
        {
            Load();
            return;
        }

        var row = elementRows[position];
        if (items.TryGetValue(row, out var old))
        {
            Unsubscribe(old);
        }

        Bind(row, item);
    }

    private void OnItemChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not TItem item)
        {
            return;
        }

        foreach (var (row, bound) in items.ToArray())
        {
            if (!ReferenceEquals(bound, item) || !sync.Sheet.Contains(row))
            {
                continue;
            }

            foreach (var link in columns)
            {
                if (string.IsNullOrEmpty(e.PropertyName) || (link.Property.Name == e.PropertyName))
                {
                    ShowCell(row, link);
                }
            }
        }
    }

    // Columns left blank show the new element's own value (for example one set by its constructor)
    private void AddItemFor(SheetRow row)
    {
        if (list is not { IsReadOnly: false })
        {
            return;
        }

        var item = Create is { } create ? create() : Activator.CreateInstance<TItem>();
        ChangeList(x => x.Insert(PositionOf(row), item));
        items[row] = item;
        remembered.AddOrUpdate(row, item);
        Subscribe(item);
        var r = row.Index;
        foreach (var link in columns)
        {
            if (IsCurrent(link) && (sync.Sheet.GetValue(r, link.Column.Index) is not null))
            {
                sync.Write(item, link.Property, r, link.Column.Index);
            }
        }

        foreach (var link in columns)
        {
            ShowCell(row, link);
        }
    }

    private void Bind(SheetRow row, TItem item)
    {
        items[row] = item;
        remembered.AddOrUpdate(row, item);
        Subscribe(item);
        foreach (var link in columns)
        {
            ShowCell(row, link);
        }
    }

    private void ClearRow(SheetRow row)
    {
        foreach (var link in columns)
        {
            if (IsCurrent(link))
            {
                sync.Clear(row.Index, link.Column.Index);
            }
        }
    }

    private void ShowCell(SheetRow row, ColumnLink link)
    {
        if (!IsCurrent(link))
        {
            return;
        }

        if (items.TryGetValue(row, out var item))
        {
            sync.Show(item, link.Property, row.Index, link.Column.Index);
        }
        else
        {
            sync.Clear(row.Index, link.Column.Index);
        }
    }

    private void InsertRowsAt(int position, int count)
    {
        var sheet = sync.Sheet;
        var index = position < rows.Count ? rows[position].Index : rows.Count > 0 ? rows[^1].Index + 1 : emptyTop;
        if (rows.Count > 0)
        {
            sheet.InsertRowsLike(index, count, position > 0 ? rows[position - 1] : rows[0]);
        }
        else
        {
            sheet.InsertRows(index, count);
        }

        var inserted = new List<SheetRow>(count);
        for (var r = index; r < index + count; r++)
        {
            inserted.Add(sheet.Rows[r]);
        }

        rows.InsertRange(position, inserted);
    }

    private void RemoveRowsAt(int position, int count)
    {
        var index = rows[position].Index;
        sync.Sheet.DeleteRows(index, count);
        foreach (var row in rows.GetRange(position, count))
        {
            if (items.Remove(row, out var item))
            {
                Unsubscribe(item);
            }
        }

        rows.RemoveRange(position, count);
        if (rows.Count == 0)
        {
            emptyTop = index;
        }
    }

    private int InsertPosition(int index, int count)
    {
        var sheet = sync.Sheet;
        if (rows.Count == 0)
        {
            return index == emptyTop ? 0 : -1;
        }

        if (index > 0)
        {
            var above = rows.IndexOf(sheet.Rows[index - 1]);
            if (above >= 0)
            {
                return above + 1;
            }
        }

        return (index + count < sheet.RowCount) && ReferenceEquals(sheet.Rows[index + count], rows[0]) ? 0 : -1;
    }

    private int PositionOf(SheetRow row)
    {
        var position = 0;
        foreach (var other in rows)
        {
            if (ReferenceEquals(other, row))
            {
                break;
            }

            if (items.ContainsKey(other))
            {
                position++;
            }
        }

        return position;
    }

    private List<SheetRow> ElementRows() => rows.Where(items.ContainsKey).ToList();

    private void ChangeList(Action<IList<TItem>> change)
    {
        changingList++;
        try
        {
            change(list!);
        }
        finally
        {
            changingList--;
        }
    }

    private void Subscribe(TItem item)
    {
        if (item is INotifyPropertyChanged notifier)
        {
            notifier.PropertyChanged += OnItemChanged;
        }
    }

    private void Unsubscribe(TItem item)
    {
        if (item is INotifyPropertyChanged notifier)
        {
            notifier.PropertyChanged -= OnItemChanged;
        }
    }

    private void DetachList()
    {
        if (list is INotifyCollectionChanged notifier)
        {
            notifier.CollectionChanged -= OnListChanged;
        }

        foreach (var item in items.Values)
        {
            Unsubscribe(item);
        }

        items.Clear();
        list = null;
    }

    private bool IsCurrent(ColumnLink link) => sync.Sheet.Contains(link.Column);

    private sealed record ColumnLink(SheetColumn Column, BoundProperty Property);
}
