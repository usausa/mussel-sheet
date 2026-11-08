namespace MusselSheet;

using MusselSheet.Internal;

public sealed partial class MusselSheetView
{
    internal async Task<int[]?> UndoFromScriptAsync(bool redo)
    {
        var target = await ApplyHistoryAsync(redo).ConfigureAwait(true);
        return target is { } range ? [range.Top, range.Left, range.Bottom, range.Right] : null;
    }

    private async Task<CellRange?> ApplyHistoryAsync(bool redo)
    {
        var entry = redo ? history.TakeRedo() : history.TakeUndo();
        var target = entry switch
        {
            CellsEdited edited => await ApplyEditAsync(edited, redo).ConfigureAwait(true),
            StructureEdited structure => ApplyStructure(structure, redo),
            _ => null
        };

        // Undo or redo may have added or removed rows, so send the new sheet map before JS moves
        await SyncMapAsync().ConfigureAwait(true);
        return target;
    }

    private async Task<CellRange?> ApplyEditAsync(CellsEdited entry, bool redo)
    {
        CellRange? target = null;
        var reason = redo ? CellChangeReason.Redo : CellChangeReason.Undo;
        var changes = redo ? entry.Changes : entry.Changes.Reverse();
        foreach (var change in changes)
        {
            if (!TryLocate(change, out var row, out var column))
            {
                continue;
            }

            var snapshot = redo ? change.After : change.Before;
            var old = Sheet.GetValue(row, column);
            Sheet.SetInput(row, column, snapshot.Value, snapshot.Input, snapshot.Error);
            target = target is { } t
                ? new CellRange(Math.Min(t.Top, row), Math.Min(t.Left, column), Math.Max(t.Bottom, row), Math.Max(t.Right, column))
                : CellRange.Cell(row, column);
            await CellCommitted.InvokeAsync(new CellCommittedEventArgs(Sheet, new CellAddress(row, column), old, snapshot.Value, snapshot.Error, reason)).ConfigureAwait(true);
        }

        // Reselects the whole area of a range operation, or the changed cells if a corner row or column is gone
        if ((entry.Area is { } area) && IsAttached(area.Top) && IsAttached(area.Bottom) && IsAttached(area.Left) && IsAttached(area.Right))
        {
            var located = new CellRange(area.Top.Index, area.Left.Index, area.Bottom.Index, area.Right.Index);
            target = target is { } t
                ? new CellRange(Math.Min(t.Top, located.Top), Math.Min(t.Left, located.Left), Math.Max(t.Bottom, located.Bottom), Math.Max(t.Right, located.Right))
                : located;
        }

        return target;
    }

    private EditArea AreaOf(CellRange range) =>
        new(Sheet.Rows[range.Top], Sheet.Columns[range.Left], Sheet.Rows[range.Bottom], Sheet.Columns[range.Right]);

    private bool TryLocate(CellChange change, out int row, out int column)
    {
        row = change.Row.Index;
        column = change.Column.Index;
        return IsAttached(change.Row)
            && (column < Sheet.ColumnCount) && ReferenceEquals(Sheet.Columns[column], change.Column);
    }
}
