namespace MusselSheet;

using MusselSheet.Internal;

public sealed partial class Sheet
{
    public void AddRows(int count, bool inheritSettings = true) => InsertRows(RowCount, count, inheritSettings);

    public void InsertRows(int index, int count, bool inheritSettings = true)
    {
        var template = index > 0 ? rowList[index - 1] : rowList.FirstOrDefault();
        var source = InheritSource(index, count, inheritSettings && (template is not null));
        var rows = CreateRows(count, inheritSettings ? template?.Height : null);
        PutRows(index, rows, RangeTransform.Insert(settings.Rules, Axis.Row, index, count, source is not null, source), RangeTransform.InsertMerges(merges, Axis.Row, index, count));
    }

    public void DeleteRows(int index, int count)
    {
        TakeRows(index, count, RangeTransform.Remove(settings.Rules, Axis.Row, index, count), RangeTransform.RemoveMerges(merges, Axis.Row, index, count));
    }

    public void AddColumns(int count, bool inheritSettings = true) => InsertColumns(ColumnCount, count, inheritSettings);

    public void InsertColumns(int index, int count, bool inheritSettings = true)
    {
        var template = index > 0 ? columnList[index - 1] : columnList.FirstOrDefault();
        var source = InheritSource(index, count, inheritSettings && (template is not null));
        var columns = CreateColumns(count, inheritSettings ? template?.Width : null);
        PutColumns(index, columns, null, RangeTransform.Insert(settings.Rules, Axis.Column, index, count, source is not null, source), RangeTransform.InsertMerges(merges, Axis.Column, index, count));
    }

    public void DeleteColumns(int index, int count)
    {
        TakeColumns(index, count, RangeTransform.Remove(settings.Rules, Axis.Column, index, count), RangeTransform.RemoveMerges(merges, Axis.Column, index, count));
    }

    internal SheetRow[] CreateRows(int count, int? height)
    {
        var rows = new SheetRow[count];
        for (var i = 0; i < count; i++)
        {
            rows[i] = new SheetRow(this, ColumnCount, height);
        }

        return rows;
    }

    internal SheetColumn[] CreateColumns(int count, int? width)
    {
        var columns = new SheetColumn[count];
        for (var i = 0; i < count; i++)
        {
            columns[i] = new SheetColumn(this, width);
        }

        return columns;
    }

    // The same row objects are put back, so history entries that point at their cells stay valid
    internal void PutRows(int index, IReadOnlyList<SheetRow> rows, IReadOnlyList<SettingsRule> rules, IReadOnlyList<CellRange> mergedRanges)
    {
        foreach (var row in rows)
        {
            if (row.Cells.Length != ColumnCount)
            {
                // Values are not carried over if the column count changed while the row was out of the sheet
                row.Cells = new CellData?[ColumnCount];
            }
        }

        rowList.InsertRange(index, rows);
        Reindex();
        settings.Replace(rules);
        ReplaceMerges(mergedRanges);
        Raise(new SheetChangedEventArgs(SheetChangeKind.RowsInserted, CellRange.Rows(index, index + rows.Count - 1), index, rows.Count));
    }

    internal void TakeRows(int index, int count, IReadOnlyList<SettingsRule> rules, IReadOnlyList<CellRange> mergedRanges)
    {
        rowList.RemoveRange(index, count);
        Reindex();
        settings.Replace(rules);
        ReplaceMerges(mergedRanges);
        Raise(new SheetChangedEventArgs(SheetChangeKind.RowsDeleted, null, index, count));
    }

    internal void PutColumns(int index, IReadOnlyList<SheetColumn> columns, IReadOnlyDictionary<SheetRow, CellData?[]>? cells, IReadOnlyList<SettingsRule> rules, IReadOnlyList<CellRange> mergedRanges)
    {
        var count = columns.Count;
        columnList.InsertRange(index, columns);
        foreach (var row in rowList)
        {
            var next = new CellData?[row.Cells.Length + count];
            Array.Copy(row.Cells, 0, next, 0, index);
            Array.Copy(row.Cells, index, next, index + count, row.Cells.Length - index);
            if ((cells is not null) && cells.TryGetValue(row, out var saved) && (saved.Length == count))
            {
                Array.Copy(saved, 0, next, index, count);
            }

            row.Cells = next;
        }

        Reindex();
        settings.Replace(rules);
        ReplaceMerges(mergedRanges);
        Raise(new SheetChangedEventArgs(SheetChangeKind.ColumnsInserted, CellRange.Columns(index, index + count - 1), index, count));
    }

    internal Dictionary<SheetRow, CellData?[]> TakeColumns(int index, int count, IReadOnlyList<SettingsRule> rules, IReadOnlyList<CellRange> mergedRanges)
    {
        var removed = new Dictionary<SheetRow, CellData?[]>();
        columnList.RemoveRange(index, count);
        foreach (var row in rowList)
        {
            var saved = new CellData?[count];
            Array.Copy(row.Cells, index, saved, 0, count);
            if (Array.Exists(saved, x => x is not null))
            {
                removed[row] = saved;
            }

            var next = new CellData?[row.Cells.Length - count];
            Array.Copy(row.Cells, 0, next, 0, index);
            Array.Copy(row.Cells, index + count, next, index, row.Cells.Length - index - count);
            row.Cells = next;
        }

        Reindex();
        settings.Replace(rules);
        ReplaceMerges(mergedRanges);
        Raise(new SheetChangedEventArgs(SheetChangeKind.ColumnsDeleted, null, index, count));
        return removed;
    }

    // Index to inherit from, counted after the insertion: the row above, or the old first row (now shifted by
    // count) when inserting at the top.
    private static int? InheritSource(int index, int count, bool inherit)
    {
        if (!inherit)
        {
            return null;
        }

        return index > 0 ? index - 1 : index + count;
    }
}
