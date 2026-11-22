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
        await HandleAsync(RowsInserting, inserting).ConfigureAwait(true);
        if (inserting.Cancel || !Sheet.Contains(anchor))
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
            _ => null
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
        var template = below ? selection.Bottom : selection.Top;
        if (template >= Sheet.RowCount)
        {
            return null;
        }

        var anchor = Sheet.Rows[template];
        var inserting = new SheetRowsInsertingEventArgs(Sheet, below ? template + 1 : template, count);
        await HandleAsync(RowsInserting, inserting).ConfigureAwait(true);
        if (inserting.Cancel || !Sheet.Contains(anchor))
        {
            return null;
        }

        var index = below ? anchor.Index + 1 : anchor.Index;
        var after = new CellRange(index, selection.Left, index + count - 1, selection.Right);
        var entry = new RowsEdited(true, index > 0 ? Sheet.Rows[index - 1] : null, Sheet.CreateRows(count, anchor.Height), index, !below, selection, after);
        PutBack(entry, after);
        history.Record(entry);
        return (after, active with { Row = index });
    }

    private async Task<(CellRange Range, CellAddress Active)?> DeleteRowsAsync(CellRange selection, CellAddress active)
    {
        var count = selection.Bottom - selection.Top + 1;
        if (count >= Sheet.RowCount)
        {
            return null;
        }

        var rows = RowsOf(selection);
        if (Array.Exists(rows, Sheet.HoldsReadOnlyItem))
        {
            return null;
        }

        var deleting = new SheetRowsDeletingEventArgs(Sheet, selection.Top, count);
        await HandleAsync(RowsDeleting, deleting).ConfigureAwait(true);
        if (deleting.Cancel || (Span(rows) is not { } span) || (count >= Sheet.RowCount) || Array.Exists(rows, Sheet.HoldsReadOnlyItem))
        {
            return null;
        }

        selection = new CellRange(span.First, selection.Left, span.Last, selection.Right);
        var entry = new RowsEdited(false, selection.Top > 0 ? Sheet.Rows[selection.Top - 1] : null, rows, selection.Top, false, selection, selection) { Present = true };
        TakeAway(entry, selection);
        history.Record(entry);
        return Clipped(selection, active);
    }

    private async Task<(CellRange Range, CellAddress Active)?> InsertColumnsAsync(CellRange selection, CellAddress active, bool right)
    {
        var count = selection.Right - selection.Left + 1;
        var template = right ? selection.Right : selection.Left;
        if (template >= Sheet.ColumnCount)
        {
            return null;
        }

        var anchor = Sheet.Columns[template];
        var inserting = new SheetColumnsInsertingEventArgs(Sheet, right ? template + 1 : template, count);
        await HandleAsync(ColumnsInserting, inserting).ConfigureAwait(true);
        if (inserting.Cancel || !Sheet.Contains(anchor))
        {
            return null;
        }

        var index = right ? anchor.Index + 1 : anchor.Index;
        var after = new CellRange(selection.Top, index, selection.Bottom, index + count - 1);
        var entry = new ColumnsEdited(true, index > 0 ? Sheet.Columns[index - 1] : null, Sheet.CreateColumns(count, anchor.Width), index, !right, selection, after);
        PutBack(entry, after);
        history.Record(entry);
        return (after, active with { Column = index });
    }

    private async Task<(CellRange Range, CellAddress Active)?> DeleteColumnsAsync(CellRange selection, CellAddress active)
    {
        var count = selection.Right - selection.Left + 1;
        if (count >= Sheet.ColumnCount)
        {
            return null;
        }

        var columns = ColumnsOf(selection);
        var deleting = new SheetColumnsDeletingEventArgs(Sheet, selection.Left, count);
        await HandleAsync(ColumnsDeleting, deleting).ConfigureAwait(true);
        if (deleting.Cancel || (Span(columns) is not { } span) || (count >= Sheet.ColumnCount))
        {
            return null;
        }

        selection = new CellRange(selection.Top, span.First, selection.Bottom, span.Last);
        var entry = new ColumnsEdited(false, selection.Left > 0 ? Sheet.Columns[selection.Left - 1] : null, columns, selection.Left, false, selection, selection) { Present = true };
        TakeAway(entry, selection);
        history.Record(entry);
        return Clipped(selection, active);
    }

    private CellRange? ApplyStructure(StructureEdited entry, bool redo, bool shifts)
    {
        var target = redo ? entry.After : entry.Before;
        var put = entry.Inserted == redo;
        historyShifts = shifts;
        try
        {
            return entry switch
            {
                RowsEdited rows => put ? PutBack(rows, target) : TakeAway(rows, target),
                ColumnsEdited columns => put ? PutBack(columns, target) : TakeAway(columns, target),
                _ => null
            };
        }
        finally
        {
            historyShifts = false;
        }
    }

    private void OwnStructure(Action change)
    {
        ownStructure++;
        try
        {
            change();
        }
        finally
        {
            ownStructure--;
        }
    }

    // Null if the rows are already in or the row above is gone; otherwise target shifted to where they went
    private CellRange? PutBack(RowsEdited entry, CellRange target)
    {
        if (entry.Present || ((entry.Above is { } above) && !Sheet.Contains(above)) || entry.Rows.Any(Sheet.Contains))
        {
            return null;
        }

        var index = entry.Above is null ? 0 : entry.Above.Index + 1;
        if (MergedAcross(entry, Axis.Row, index))
        {
            return null;
        }

        var before = Sheet.Rules.ToArray();
        var rules = RulesToPut(entry, Axis.Row, index, entry.Rows.Count, Sheet.RowCount);
        var (merges, mergesBefore) = MergesToPut(entry, Axis.Row, index, entry.Rows.Count);
        OwnStructure(() => Sheet.PutRows(index, entry.Rows, rules, merges));
        (entry.RulesWith, entry.RulesWithout, entry.Present) = (rules, before, true);
        (entry.MergesWith, entry.MergesWithout) = (merges, mergesBefore);
        return Shift(target, Axis.Row, index - entry.Index);
    }

    // Null if the rows are not in the sheet, the app removed some or inserted rows between them, or one holds an element of a read-only list
    private CellRange? TakeAway(RowsEdited entry, CellRange target)
    {
        if (!entry.Present || (Span(entry.Rows) is null) || entry.Rows.Any(Sheet.HoldsReadOnlyItem))
        {
            return null;
        }

        var (index, count) = (entry.Rows[0].Index, entry.Rows.Count);
        var before = Sheet.Rules.ToArray();
        var rules = RulesToTake(entry, Axis.Row, index, count);
        var (merges, mergesBefore) = MergesToTake(entry, Axis.Row, index, count);
        OwnStructure(() => Sheet.TakeRows(index, count, rules, merges));
        (entry.RulesWith, entry.RulesWithout, entry.Present) = (before, rules, false);
        (entry.MergesWith, entry.MergesWithout) = (mergesBefore, merges);
        return Shift(target, Axis.Row, index - entry.Index);
    }

    private CellRange? PutBack(ColumnsEdited entry, CellRange target)
    {
        if (entry.Present || ((entry.Left is { } left) && !Sheet.Contains(left)) || entry.Columns.Any(Sheet.Contains))
        {
            return null;
        }

        var index = entry.Left is null ? 0 : entry.Left.Index + 1;
        if (MergedAcross(entry, Axis.Column, index))
        {
            return null;
        }

        var before = Sheet.Rules.ToArray();
        var rules = RulesToPut(entry, Axis.Column, index, entry.Columns.Count, Sheet.ColumnCount);
        var (merges, mergesBefore) = MergesToPut(entry, Axis.Column, index, entry.Columns.Count);
        OwnStructure(() => Sheet.PutColumns(index, entry.Columns, entry.Cells, rules, merges));
        (entry.RulesWith, entry.RulesWithout, entry.Present, entry.Cells) = (rules, before, true, null);
        (entry.MergesWith, entry.MergesWithout) = (merges, mergesBefore);
        return Shift(target, Axis.Column, index - entry.Index);
    }

    private CellRange? TakeAway(ColumnsEdited entry, CellRange target)
    {
        if (!entry.Present || (Span(entry.Columns) is null))
        {
            return null;
        }

        var (index, count) = (entry.Columns[0].Index, entry.Columns.Count);
        var before = Sheet.Rules.ToArray();
        var rules = RulesToTake(entry, Axis.Column, index, count);
        var (merges, mergesBefore) = MergesToTake(entry, Axis.Column, index, count);
        OwnStructure(() => entry.Cells = Sheet.TakeColumns(index, count, rules, merges));
        (entry.RulesWith, entry.RulesWithout, entry.Present) = (before, rules, false);
        (entry.MergesWith, entry.MergesWithout) = (mergesBefore, merges);
        return Shift(target, Axis.Column, index - entry.Index);
    }

    private void FollowStructure(SheetChangedEventArgs e)
    {
        var axis = e.Kind is SheetChangeKind.RowsInserted or SheetChangeKind.RowsDeleted ? Axis.Row : Axis.Column;
        var inserted = e.Kind is SheetChangeKind.RowsInserted or SheetChangeKind.ColumnsInserted;
        FollowRules(history.UndoOrder, e, axis, inserted);
        FollowRules(history.RedoOrder, e, axis, inserted);
        FollowMerges(history.UndoOrder, e, axis, inserted);
        FollowMerges(history.RedoOrder, e, axis, inserted);
    }

    private void FollowRules(IEnumerable<EditEntry> entries, SheetChangedEventArgs e, Axis axis, bool inserted)
    {
        var rules = Sheet.Rules;
        var (index, source) = (e.Index, e.InheritedFrom);
        var following = true;
        foreach (var step in entries.OfType<StructureEdited>())
        {
            if (following && (step.RulesWith is { } with) && (step.RulesWithout is { } without))
            {
                var current = Moved(step.Present ? with : without, axis, inserted, index, e.Count, source);
                if (rules.Take(current.Count).SequenceEqual(current) && (OtherSide(step, e, axis, index, source) is { FromStep: false } other))
                {
                    IReadOnlyList<SettingsRule> kept = rules.Take(current.Count).ToArray();
                    IReadOnlyList<SettingsRule> moved = Moved(step.Present ? without : with, axis, inserted, other.Index, e.Count, other.Source);
                    (step.RulesWith, step.RulesWithout) = step.Present ? (kept, moved) : (moved, kept);
                    (rules, index, source) = (moved, other.Index, other.Source);
                    continue;
                }
            }

            following = false;
            (step.RulesWith, step.RulesWithout) = (null, null);
        }
    }

    private void FollowMerges(IEnumerable<EditEntry> entries, SheetChangedEventArgs e, Axis axis, bool inserted)
    {
        var index = e.Index;
        var following = true;
        foreach (var step in entries.OfType<StructureEdited>())
        {
            if (following && (step.MergesWith is { } with) && (step.MergesWithout is { } without) && (OtherSide(step, e, axis, index, null) is { } other))
            {
                IReadOnlyList<CellRange> current = MovedMerges(step.Present ? with : without, axis, inserted, index, e.Count);
                IReadOnlyList<CellRange> moved = MovedMerges(step.Present ? without : with, axis, inserted, other.Index, e.Count);
                (step.MergesWith, step.MergesWithout) = step.Present ? (current, moved) : (moved, current);
                index = other.Index;
                continue;
            }

            following = false;
            (step.MergesWith, step.MergesWithout) = (null, null);
        }
    }

    private (int Index, int? Source, bool FromStep)? OtherSide(StructureEdited step, SheetChangedEventArgs e, Axis axis, int index, int? source)
    {
        if ((step is RowsEdited) != (axis == Axis.Row))
        {
            return (index, source, false);
        }

        if (PlaceOf(step) is not { } place)
        {
            return null;
        }

        var fromStep = (e.InheritedFrom is { } from) && (from >= place.First) && (from <= place.Last);
        return (e.Index > place.Last ? index + place.Shift : index, e.InheritedFrom > place.Last ? source + place.Shift : source, fromStep);
    }

    private (int First, int Last, int Shift)? PlaceOf(StructureEdited step) => step switch
    {
        RowsEdited { Present: true } rows => Span(rows.Rows) is { } span ? (span.First, span.Last, -rows.Rows.Count) : null,
        RowsEdited rows => (((rows.Above is { } above) && !Sheet.Contains(above)) || rows.Rows.Any(Sheet.Contains)) ? null : Gap(rows.Above?.Index ?? -1, rows.Rows.Count),
        ColumnsEdited { Present: true } columns => Span(columns.Columns) is { } span ? (span.First, span.Last, -columns.Columns.Count) : null,
        ColumnsEdited columns => (((columns.Left is { } left) && !Sheet.Contains(left)) || columns.Columns.Any(Sheet.Contains)) ? null : Gap(columns.Left?.Index ?? -1, columns.Columns.Count),
        _ => null
    };

    private static (int First, int Last, int Shift) Gap(int after, int count) => (after + 1, after, count);

    private static List<SettingsRule> Moved(IReadOnlyList<SettingsRule> rules, Axis axis, bool inserted, int index, int count, int? source) =>
        inserted ? RangeTransform.Insert(rules, axis, index, count, source is not null, source) : RangeTransform.Remove(rules, axis, index, count);

    private static List<CellRange> MovedMerges(IReadOnlyList<CellRange> merges, Axis axis, bool inserted, int index, int count) =>
        inserted ? RangeTransform.InsertMerges(merges, axis, index, count) : RangeTransform.RemoveMerges(merges, axis, index, count);

    private bool MergedAcross(StructureEdited entry, Axis axis, int index) =>
        (entry.MergesWithout is { } without) && Sheet.MergeAreas.Any(x => Straddles(x, axis, index) && !without.Contains(x));

    private static bool Straddles(CellRange merge, Axis axis, int index) =>
        axis == Axis.Row ? (merge.Top < index) && (merge.Bottom >= index) : (merge.Left < index) && (merge.Right >= index);

    // The earlier rules if unchanged, plus shifted ones the app appended; otherwise inherited from the neighbor
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

    // The earlier merges if unchanged, which brings back dropped ones; otherwise the current ones shifted, with the step's own merges where no merge is
    private (IReadOnlyList<CellRange> Next, IReadOnlyList<CellRange> Before) MergesToPut(StructureEdited entry, Axis axis, int index, int count)
    {
        var before = Sheet.MergeAreas.ToArray();
        if ((entry.MergesWith is { } with) && (entry.MergesWithout is { } without) && before.SequenceEqual(without))
        {
            return (with, before);
        }

        var next = RangeTransform.InsertMerges(before, axis, index, count);
        foreach (var merge in entry.MergesWith ?? [])
        {
            var left = RangeTransform.RemoveMerges([merge], axis, index, count);
            if ((left is [var kept]) && !before.Contains(kept))
            {
                continue;
            }

            CellRange? current = left is [var image] ? RangeTransform.InsertMerges([image], axis, index, count)[0] : null;
            if ((current == merge) || next.Exists(x => (x != current) && x.Intersects(merge)))
            {
                continue;
            }

            if (current is { } old)
            {
                next.Remove(old);
            }

            next.Add(merge);
        }

        return (next, before);
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
}
