namespace MusselSheet;

using MusselSheet.Internal;

public sealed partial class MusselSheetView
{
    // The sheet map is sent before returning, because JS moves to the new row right away
    internal async Task<bool> AddRowFromScriptAsync(int row, int column)
    {
        if ((LastRowEnterMode != SheetLastRowEnterMode.AddRow) || !IsEditable(row, column))
        {
            return false;
        }

        var anchor = Sheet.Rows[row];
        var inserting = new SheetRowsInsertingEventArgs(Sheet, row + 1, 1);
        await RowsInserting.InvokeAsync(inserting).ConfigureAwait(true);
        if (inserting.Cancel || !IsAttached(anchor))
        {
            return false;
        }

        // Undo returns to the cell where Enter was pressed; redo goes back to the added row
        var index = anchor.Index + 1;
        var entry = new RowsEdited(true, anchor, Sheet.CreateRows(1, anchor.Height), index, false, CellRange.Cell(anchor.Index, column), CellRange.Cell(index, column));
        PutBack(entry, entry.After);
        history.Record(entry);
        await SyncMapAsync().ConfigureAwait(true);
        return true;
    }

    // The sheet map is sent before returning, because JS selects the returned range right away
    internal async Task<int[]?> ChangeStructureAsync(string action, int top, int left, int bottom, int right, int row, int column)
    {
        if ((Sheet.RowCount == 0) || (Sheet.ColumnCount == 0))
        {
            return null;
        }

        var selection = ClipToSheet(new CellRange(top, left, bottom, right));
        var active = new CellAddress(Math.Clamp(row, selection.Top, selection.Bottom), Math.Clamp(column, selection.Left, selection.Right));
        var result = action switch
        {
            "insertRowsAbove" when AllowInsertRows => await InsertRowsAsync(selection, active, false).ConfigureAwait(true),
            "insertRowsBelow" when AllowInsertRows => await InsertRowsAsync(selection, active, true).ConfigureAwait(true),
            "deleteRows" when AllowDeleteRows => await DeleteRowsAsync(selection, active).ConfigureAwait(true),
            "insertColumnsLeft" when AllowInsertColumns => await InsertColumnsAsync(selection, active, false).ConfigureAwait(true),
            "insertColumnsRight" when AllowInsertColumns => await InsertColumnsAsync(selection, active, true).ConfigureAwait(true),
            "deleteColumns" when AllowDeleteColumns => await DeleteColumnsAsync(selection, active).ConfigureAwait(true),
            _ => null,
        };
        if (result is not { } after)
        {
            return null;
        }

        await SyncMapAsync().ConfigureAwait(true);
        var (range, cell) = after;
        return [range.Top, range.Left, range.Bottom, range.Right, cell.Row, cell.Column];
    }

    private async Task<(CellRange Range, CellAddress Active)?> InsertRowsAsync(CellRange selection, CellAddress active, bool below)
    {
        var count = selection.Bottom - selection.Top + 1;
        var index = below ? selection.Bottom + 1 : selection.Top;
        var inserting = new SheetRowsInsertingEventArgs(Sheet, index, count);
        await RowsInserting.InvokeAsync(inserting).ConfigureAwait(true);
        var template = below ? selection.Bottom : selection.Top;
        if (inserting.Cancel || (template >= Sheet.RowCount))
        {
            return null;
        }

        var after = new CellRange(index, selection.Left, index + count - 1, selection.Right);
        var entry = new RowsEdited(true, index > 0 ? Sheet.Rows[index - 1] : null, Sheet.CreateRows(count, Sheet.Rows[template].Height), index, !below, selection, after);
        PutBack(entry, after);
        history.Record(entry);
        return (after, new CellAddress(index, active.Column));
    }

    private async Task<(CellRange Range, CellAddress Active)?> DeleteRowsAsync(CellRange selection, CellAddress active)
    {
        var count = selection.Bottom - selection.Top + 1;
        if (count >= Sheet.RowCount)
        {
            return null;
        }

        var deleting = new SheetRowsDeletingEventArgs(Sheet, selection.Top, count);
        await RowsDeleting.InvokeAsync(deleting).ConfigureAwait(true);
        if (deleting.Cancel || (selection.Bottom >= Sheet.RowCount) || (count >= Sheet.RowCount))
        {
            return null;
        }

        var rows = Sheet.Rows.Skip(selection.Top).Take(count).ToArray();
        var entry = new RowsEdited(false, selection.Top > 0 ? Sheet.Rows[selection.Top - 1] : null, rows, selection.Top, false, selection, selection) { Present = true };
        TakeAway(entry, selection);
        history.Record(entry);
        return Clipped(selection, active);
    }

    private async Task<(CellRange Range, CellAddress Active)?> InsertColumnsAsync(CellRange selection, CellAddress active, bool right)
    {
        var count = selection.Right - selection.Left + 1;
        var index = right ? selection.Right + 1 : selection.Left;
        var inserting = new SheetColumnsInsertingEventArgs(Sheet, index, count);
        await ColumnsInserting.InvokeAsync(inserting).ConfigureAwait(true);
        var template = right ? selection.Right : selection.Left;
        if (inserting.Cancel || (template >= Sheet.ColumnCount))
        {
            return null;
        }

        var after = new CellRange(selection.Top, index, selection.Bottom, index + count - 1);
        var entry = new ColumnsEdited(true, index > 0 ? Sheet.Columns[index - 1] : null, Sheet.CreateColumns(count, Sheet.Columns[template].Width), index, !right, selection, after);
        PutBack(entry, after);
        history.Record(entry);
        return (after, new CellAddress(active.Row, index));
    }

    private async Task<(CellRange Range, CellAddress Active)?> DeleteColumnsAsync(CellRange selection, CellAddress active)
    {
        var count = selection.Right - selection.Left + 1;
        if (count >= Sheet.ColumnCount)
        {
            return null;
        }

        var deleting = new SheetColumnsDeletingEventArgs(Sheet, selection.Left, count);
        await ColumnsDeleting.InvokeAsync(deleting).ConfigureAwait(true);
        if (deleting.Cancel || (selection.Right >= Sheet.ColumnCount) || (count >= Sheet.ColumnCount))
        {
            return null;
        }

        var columns = Sheet.Columns.Skip(selection.Left).Take(count).ToArray();
        var entry = new ColumnsEdited(false, selection.Left > 0 ? Sheet.Columns[selection.Left - 1] : null, columns, selection.Left, false, selection, selection) { Present = true };
        TakeAway(entry, selection);
        history.Record(entry);
        return Clipped(selection, active);
    }

    private CellRange? ApplyStructure(StructureEdited entry, bool redo)
    {
        var target = redo ? entry.After : entry.Before;
        var put = entry.Inserted == redo;
        return entry switch
        {
            RowsEdited rows => put ? PutBack(rows, target) : TakeAway(rows, target),
            ColumnsEdited columns => put ? PutBack(columns, target) : TakeAway(columns, target),
            _ => null,
        };
    }

    // Does nothing if the rows are already in or the row above them has been removed; otherwise returns target
    // shifted to where they were put.
    private CellRange? PutBack(RowsEdited entry, CellRange target)
    {
        if (entry.Present || ((entry.Above is { } above) && !IsAttached(above)) || entry.Rows.Any(IsAttached))
        {
            return null;
        }

        var index = entry.Above is null ? 0 : entry.Above.Index + 1;
        var before = Sheet.Rules.ToArray();
        var rules = RulesToPut(entry, Axis.Row, index, entry.Rows.Count, Sheet.RowCount);
        var (merges, mergesBefore) = MergesToPut(entry, Axis.Row, index, entry.Rows.Count);
        Sheet.PutRows(index, entry.Rows, rules, merges);
        (entry.RulesWith, entry.RulesWithout, entry.Present) = (rules, before, true);
        (entry.MergesWith, entry.MergesWithout) = (merges, mergesBefore);
        return Shift(target, Axis.Row, index - entry.Index);
    }

    // Does nothing if the rows are not in the sheet, or the app removed some of them or inserted rows between them
    private CellRange? TakeAway(RowsEdited entry, CellRange target)
    {
        if (!entry.Present || !IsRun(entry.Rows))
        {
            return null;
        }

        var (index, count) = (entry.Rows[0].Index, entry.Rows.Count);
        var before = Sheet.Rules.ToArray();
        var rules = RulesToTake(entry, Axis.Row, index, count);
        var (merges, mergesBefore) = MergesToTake(entry, Axis.Row, index, count);
        Sheet.TakeRows(index, count, rules, merges);
        (entry.RulesWith, entry.RulesWithout, entry.Present) = (before, rules, false);
        (entry.MergesWith, entry.MergesWithout) = (mergesBefore, merges);
        return Shift(target, Axis.Row, index - entry.Index);
    }

    private CellRange? PutBack(ColumnsEdited entry, CellRange target)
    {
        if (entry.Present || ((entry.Left is { } left) && !IsAttached(left)) || entry.Columns.Any(IsAttached))
        {
            return null;
        }

        var index = entry.Left is null ? 0 : entry.Left.Index + 1;
        var before = Sheet.Rules.ToArray();
        var rules = RulesToPut(entry, Axis.Column, index, entry.Columns.Count, Sheet.ColumnCount);
        var (merges, mergesBefore) = MergesToPut(entry, Axis.Column, index, entry.Columns.Count);
        Sheet.PutColumns(index, entry.Columns, entry.Cells, rules, merges);
        (entry.RulesWith, entry.RulesWithout, entry.Present, entry.Cells) = (rules, before, true, null);
        (entry.MergesWith, entry.MergesWithout) = (merges, mergesBefore);
        return Shift(target, Axis.Column, index - entry.Index);
    }

    private CellRange? TakeAway(ColumnsEdited entry, CellRange target)
    {
        if (!entry.Present || !IsRun(entry.Columns))
        {
            return null;
        }

        var (index, count) = (entry.Columns[0].Index, entry.Columns.Count);
        var before = Sheet.Rules.ToArray();
        var rules = RulesToTake(entry, Axis.Column, index, count);
        var (merges, mergesBefore) = MergesToTake(entry, Axis.Column, index, count);
        entry.Cells = Sheet.TakeColumns(index, count, rules, merges);
        (entry.RulesWith, entry.RulesWithout, entry.Present) = (before, rules, false);
        (entry.MergesWith, entry.MergesWithout) = (mergesBefore, merges);
        return Shift(target, Axis.Column, index - entry.Index);
    }

    // If the rules are unchanged since the removal, restore the earlier ones as they were. If the app only appended
    // rules since then, shift those and add them after. Otherwise inherit from the neighboring row or column
    // (the next one when inserting above or to the left).
    private List<SettingsRule> RulesToPut(StructureEdited entry, Axis axis, int index, int count, int total)
    {
        var current = Sheet.Rules;
        if ((entry.RulesWith is { } with) && (entry.RulesWithout is { } without) && StartsWith(current, without))
        {
            return [.. with, .. RangeTransform.Insert([.. current.Skip(without.Count)], axis, index, count, false, null)];
        }

        int? source = entry.InheritFromNext ? index + count : index - 1;
        if ((source < 0) || (source >= total + count))
        {
            source = entry.InheritFromNext ? index - 1 : index + count;
        }

        if ((source < 0) || (source >= total + count))
        {
            source = null;
        }

        return RangeTransform.Insert(current, axis, index, count, source is not null, source);
    }

    private List<SettingsRule> RulesToTake(StructureEdited entry, Axis axis, int index, int count)
    {
        var current = Sheet.Rules;
        if ((entry.RulesWith is { } with) && (entry.RulesWithout is { } without) && StartsWith(current, with))
        {
            return [.. without, .. RangeTransform.Remove([.. current.Skip(with.Count)], axis, index, count)];
        }

        return RangeTransform.Remove(current, axis, index, count);
    }

    // If the merges are unchanged since the removal, restore the earlier ones exactly, which also brings back merges
    // the deletion dropped; otherwise shift the current merges to match the insertion.
    private (IReadOnlyList<CellRange> Next, IReadOnlyList<CellRange> Before) MergesToPut(StructureEdited entry, Axis axis, int index, int count)
    {
        var before = Sheet.MergeAreas.ToArray();
        if ((entry.MergesWith is { } with) && (entry.MergesWithout is { } without) && before.SequenceEqual(without))
        {
            return (with, before);
        }

        return (RangeTransform.InsertMerges(before, axis, index, count), before);
    }

    private (IReadOnlyList<CellRange> Next, IReadOnlyList<CellRange> Before) MergesToTake(StructureEdited entry, Axis axis, int index, int count)
    {
        var before = Sheet.MergeAreas.ToArray();
        if ((entry.MergesWith is { } with) && (entry.MergesWithout is { } without) && before.SequenceEqual(with))
        {
            return (without, before);
        }

        return (RangeTransform.RemoveMerges(before, axis, index, count), before);
    }

    // Compared by reference: any row or column change or added rule produces new rule objects
    private static bool StartsWith(IReadOnlyList<SettingsRule> rules, IReadOnlyList<SettingsRule> prefix)
    {
        if (rules.Count < prefix.Count)
        {
            return false;
        }

        for (var i = 0; i < prefix.Count; i++)
        {
            if (!ReferenceEquals(rules[i], prefix[i]))
            {
                return false;
            }
        }

        return true;
    }

    private bool IsRun(IReadOnlyList<SheetRow> rows) =>
        (rows.Count > 0) && rows.Select((row, i) => IsAttached(row) && (row.Index == rows[0].Index + i)).All(x => x);

    private bool IsRun(IReadOnlyList<SheetColumn> columns) =>
        (columns.Count > 0) && columns.Select((column, i) => IsAttached(column) && (column.Index == columns[0].Index + i)).All(x => x);

    private CellRange? Shift(CellRange range, Axis axis, int delta)
    {
        if ((Sheet.RowCount == 0) || (Sheet.ColumnCount == 0))
        {
            return null;
        }

        return ClipToSheet(axis == Axis.Row
            ? new CellRange(range.Top + delta, range.Left, range.Bottom + delta, range.Right)
            : new CellRange(range.Top, range.Left + delta, range.Bottom, range.Right + delta));
    }

    private (CellRange Range, CellAddress Active) Clipped(CellRange range, CellAddress active)
    {
        var clipped = ClipToSheet(range);
        return (clipped, new CellAddress(Math.Clamp(active.Row, clipped.Top, clipped.Bottom), Math.Clamp(active.Column, clipped.Left, clipped.Right)));
    }

    private CellRange ClipToSheet(CellRange range)
    {
        var top = Math.Clamp(range.Top, 0, Sheet.RowCount - 1);
        var left = Math.Clamp(range.Left, 0, Sheet.ColumnCount - 1);
        return new CellRange(top, left, Math.Clamp(range.Bottom, top, Sheet.RowCount - 1), Math.Clamp(range.Right, left, Sheet.ColumnCount - 1));
    }

    private bool IsAttached(SheetRow row) =>
        (row.Index < Sheet.RowCount) && ReferenceEquals(Sheet.Rows[row.Index], row);

    private bool IsAttached(SheetColumn column) =>
        (column.Index < Sheet.ColumnCount) && ReferenceEquals(Sheet.Columns[column.Index], column);
}
