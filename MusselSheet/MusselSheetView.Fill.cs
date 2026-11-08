namespace MusselSheet;

using MusselSheet.Internal;

public sealed partial class MusselSheetView
{
    internal async Task<PasteResult> FillAsync(int top, int left, int bottom, int right, string text)
    {
        if ((Sheet.RowCount == 0) || (Sheet.ColumnCount == 0))
        {
            return new PasteResult(top, left, bottom, right, null);
        }

        var range = ClipToSheet(new CellRange(top, left, bottom, right));
        var (marked, rejected) = await InputCellsAsync(range, (_, _) => text, CellChangeReason.Input).ConfigureAwait(true);
        return new PasteResult(range.Top, range.Left, range.Bottom, range.Right, PasteMessage(marked, rejected, 0, 0));
    }

    internal Task<PasteResult> FillDownAsync(int top, int left, int bottom, int right) =>
        FillFromEdgeAsync(new CellRange(top, left, bottom, right), true);

    internal Task<PasteResult> FillRightAsync(int top, int left, int bottom, int right) =>
        FillFromEdgeAsync(new CellRange(top, left, bottom, right), false);

    internal Task<PasteResult> AutoFillAsync(int top, int left, int bottom, int right, int toTop, int toLeft, int toBottom, int toRight, bool toggle) =>
        AutoFillCoreAsync(new CellRange(top, left, bottom, right), new CellRange(toTop, toLeft, toBottom, toRight), toggle);

    internal Task<PasteResult> AutoFillDownAsync(int top, int left, int bottom, int right)
    {
        if ((Sheet.RowCount == 0) || (Sheet.ColumnCount == 0))
        {
            return Task.FromResult(new PasteResult(top, left, bottom, right, null));
        }

        var source = ClipToSheet(new CellRange(top, left, bottom, right));
        return AutoFillCoreAsync(source, new CellRange(source.Top, source.Left, FillEnd(source), source.Right), false);
    }

    internal int[] FindEdge(int row, int column, string direction)
    {
        if (!IsInSheet(row, column))
        {
            return [Math.Max(row, 0), Math.Max(column, 0)];
        }

        if (direction == "end")
        {
            return LastUsedCell();
        }

        var (dr, dc) = direction switch
        {
            "up" => (-1, 0),
            "down" => (1, 0),
            "left" => (0, -1),
            _ => (0, 1)
        };
        var (r, c) = (row + dr, column + dc);
        if (!IsInSheet(r, c))
        {
            return [row, column];
        }

        if (!IsEmpty(row, column) && !IsEmpty(r, c))
        {
            while (IsInSheet(r + dr, c + dc) && !IsEmpty(r + dr, c + dc))
            {
                (r, c) = (r + dr, c + dc);
            }

            return [r, c];
        }

        while (IsEmpty(r, c) && IsInSheet(r + dr, c + dc))
        {
            (r, c) = (r + dr, c + dc);
        }

        return [r, c];
    }

    // Copies the source cell's edit text, so each target converts and validates it with its own rules.
    // With one row (column) selected the source is the row above (column to the left), otherwise the first one.
    private async Task<PasteResult> FillFromEdgeAsync(CellRange selection, bool down)
    {
        if ((Sheet.RowCount == 0) || (Sheet.ColumnCount == 0))
        {
            return new PasteResult(selection.Top, selection.Left, selection.Bottom, selection.Right, null);
        }

        selection = ClipToSheet(selection);
        var single = down ? selection.Top == selection.Bottom : selection.Left == selection.Right;
        var source = (down ? selection.Top : selection.Left) - (single ? 1 : 0);
        var range = (single, down) switch
        {
            (true, _) => selection,
            (false, true) => new CellRange(selection.Top + 1, selection.Left, selection.Bottom, selection.Right),
            _ => new CellRange(selection.Top, selection.Left + 1, selection.Bottom, selection.Right)
        };
        if (source < 0)
        {
            return new PasteResult(selection.Top, selection.Left, selection.Bottom, selection.Right, null);
        }

        var (marked, rejected) = await InputCellsAsync(
            range,
            (r, c) => down ? Sheet.GetEditText(source, c) : Sheet.GetEditText(r, source),
            CellChangeReason.Fill,
            selection).ConfigureAwait(true);
        return new PasteResult(selection.Top, selection.Left, selection.Bottom, selection.Right, PasteMessage(marked, rejected, 0, 0));
    }

    private async Task<PasteResult> AutoFillCoreAsync(CellRange source, CellRange whole, bool toggle)
    {
        if (!FillHandleEnabled || (Sheet.RowCount == 0) || (Sheet.ColumnCount == 0))
        {
            return new PasteResult(source.Top, source.Left, source.Bottom, source.Right, null);
        }

        source = ClipToSheet(source);
        if (FillTarget(source, ClipToSheet(whole), out var vertical) is not { } target)
        {
            return new PasteResult(source.Top, source.Left, source.Bottom, source.Right, null);
        }

        var filling = new SheetFillingEventArgs(Sheet, source, target);
        await Filling.InvokeAsync(filling).ConfigureAwait(true);
        if (filling.Cancel)
        {
            return new PasteResult(source.Top, source.Left, source.Bottom, source.Right, null);
        }

        whole = new CellRange(Math.Min(source.Top, target.Top), Math.Min(source.Left, target.Left), Math.Max(source.Bottom, target.Bottom), Math.Max(source.Right, target.Right));
        var count = vertical ? source.Bottom - source.Top + 1 : source.Right - source.Left + 1;
        var lines = new Dictionary<int, Func<int, object?>?>();
        var (marked, rejected) = await InputCellsAsync(target, TextAt, CellChangeReason.Fill, whole).ConfigureAwait(true);
        return new PasteResult(whole.Top, whole.Left, whole.Bottom, whole.Right, PasteMessage(marked, rejected, 0, 0));

        string? TextAt(int row, int column)
        {
            var line = vertical ? column : row;
            if (!lines.TryGetValue(line, out var series))
            {
                var values = new object?[count];
                for (var i = 0; i < count; i++)
                {
                    values[i] = vertical ? Sheet.GetValue(source.Top + i, column) : Sheet.GetValue(row, source.Left + i);
                }

                series = FillSeries.Create(values, toggle);
                lines[line] = series;
            }

            var position = vertical ? row - source.Top : column - source.Left;
            if (series is null)
            {
                var index = FillSeries.Mod(position, count);
                return vertical ? Sheet.GetEditText(source.Top + index, column) : Sheet.GetEditText(row, source.Left + index);
            }

            return series(position) is { } value ? CellText.Edit(value, Sheet.GetSettings(row, column), Sheet.Culture, Reading) : null;
        }
    }

    private static CellRange? FillTarget(CellRange source, CellRange whole, out bool vertical)
    {
        vertical = true;
        if (whole.Bottom > source.Bottom)
        {
            return new CellRange(source.Bottom + 1, source.Left, whole.Bottom, source.Right);
        }

        if (whole.Top < source.Top)
        {
            return new CellRange(whole.Top, source.Left, source.Top - 1, source.Right);
        }

        vertical = false;
        if (whole.Right > source.Right)
        {
            return new CellRange(source.Top, source.Right + 1, source.Bottom, whole.Right);
        }

        return whole.Left < source.Left ? new CellRange(source.Top, whole.Left, source.Bottom, source.Left - 1) : null;
    }

    // Mirrors Excel: follows the adjacent column (left, else right) while it has data, and stops before a row whose
    // target cells already hold a value, so nothing is overwritten.
    private int FillEnd(CellRange source)
    {
        var below = source.Bottom + 1;
        if (below >= Sheet.RowCount)
        {
            return source.Bottom;
        }

        var column = (source.Left > 0) && !IsEmpty(below, source.Left - 1) ? source.Left - 1
            : (source.Right + 1 < Sheet.ColumnCount) && !IsEmpty(below, source.Right + 1) ? source.Right + 1
            : -1;
        var end = source.Bottom;
        while ((column >= 0) && (end + 1 < Sheet.RowCount) && !IsEmpty(end + 1, column) && IsEmptyRow(end + 1, source.Left, source.Right))
        {
            end++;
        }

        return end;
    }

    private bool IsEmptyRow(int row, int left, int right)
    {
        for (var c = left; c <= right; c++)
        {
            if (!IsEmpty(row, c))
            {
                return false;
            }
        }

        return true;
    }

    private bool IsEmpty(int row, int column) => Sheet.GetValue(row, column) is null or "";

    private int[] LastUsedCell()
    {
        var (bottom, right) = (0, 0);
        for (var r = 0; r < Sheet.RowCount; r++)
        {
            for (var c = 0; c < Sheet.ColumnCount; c++)
            {
                if (!IsEmpty(r, c))
                {
                    bottom = Math.Max(bottom, r);
                    right = Math.Max(right, c);
                }
            }
        }

        return [bottom, right];
    }
}
