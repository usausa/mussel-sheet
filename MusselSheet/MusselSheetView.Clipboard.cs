namespace MusselSheet;

using System.Globalization;
using System.Text;

using Microsoft.JSInterop;

using MusselSheet.Internal;

public sealed partial class MusselSheetView
{
    internal Task<CommitResult> PasteAsync(int row, int column, string text, bool optimistic) =>
        CommitCoreAsync(row, column, text, optimistic, CellChangeReason.Paste);

    // JS copies to the clipboard; this only clears the value, same as Delete
    internal Task<CommitResult> CutAsync(int row, int column) =>
        CommitCoreAsync(row, column, string.Empty, true, CellChangeReason.Cut);

    // JS sends the text as a stream: it can be large, and Interactive Server limits a message to 32 KB
    internal async Task<PasteResult> PasteRangeAsync(int top, int left, int bottom, int right, IJSStreamReference data) =>
        await ReadTextAsync(data).ConfigureAwait(true) is { } text
            ? await PasteTextAsync(new CellRange(top, left, bottom, right), text).ConfigureAwait(true)
            : RefuseLargeText(top, left, bottom, right);

    internal static async Task<string?> ReadTextAsync(IJSStreamReference? data)
    {
        if (data is null)
        {
            return string.Empty;
        }

        try
        {
            if (data.Length > MaxPasteBytes)
            {
                return null;
            }

            var stream = await data.OpenReadStreamAsync(MaxPasteBytes).ConfigureAwait(true);
            await using (stream.ConfigureAwait(true))
            {
                using var reader = new StreamReader(stream, Encoding.UTF8);
                return await reader.ReadToEndAsync().ConfigureAwait(true);
            }
        }
        finally
        {
            await data.DisposeAsync().ConfigureAwait(true);
        }
    }

    // As for a cancel, nothing is applied, and an optimistic cell is marked so the failure stays visible
    internal CommitResult RefuseLargeText(int row, int column, bool optimistic)
    {
        if (!IsEditable(row, column))
        {
            return Rejected;
        }

        if (optimistic)
        {
            Sheet.SetError(row, column, Texts.TextTooLarge);
        }

        return new CommitResult(false, Texts.TextTooLarge, false);
    }

    internal PasteResult RefuseLargeText(int top, int left, int bottom, int right) =>
        new(top, left, bottom, right, Texts.TextTooLarge);

    internal Task ClearRangeAsync(int top, int left, int bottom, int right) =>
        ClearCellsAsync(new CellRange(top, left, bottom, right), CellChangeReason.ClearContents);

    internal Task CutRangeAsync(int top, int left, int bottom, int right) =>
        ClearCellsAsync(new CellRange(top, left, bottom, right), CellChangeReason.Cut);

    // Called by JS when the copied range includes rows that are not rendered
    internal ClipboardText GetRangeText(int top, int left, int bottom, int right)
    {
        if ((Sheet.RowCount == 0) || (Sheet.ColumnCount == 0))
        {
            return new ClipboardText(string.Empty, string.Empty);
        }

        var range = ClipToSheet(new CellRange(top, left, bottom, right));
        var rows = new List<IReadOnlyList<string>>();
        for (var r = range.Top; r <= range.Bottom; r++)
        {
            var cells = new string[range.Right - range.Left + 1];
            for (var c = range.Left; c <= range.Right; c++)
            {
                cells[c - range.Left] = Sheet.GetEditText(r, c);
            }

            rows.Add(cells);
        }

        var html = ClipboardFormat.ToHtml(rows, (r, c) => IsTextKind(Sheet.GetSettings(range.Top + r, range.Left + c).Kind));
        return new ClipboardText(ClipboardFormat.ToTsv(rows), html);
    }

    internal async Task<PasteResult> PasteTextAsync(CellRange selection, string text)
    {
        var values = ClipboardFormat.Parse(text);
        if ((values.Count == 0) || (Sheet.RowCount == 0) || (Sheet.ColumnCount == 0))
        {
            return new PasteResult(selection.Top, selection.Left, selection.Bottom, selection.Right, null);
        }

        selection = ClipToSheet(selection);
        var width = values.Max(x => x.Length);
        var fill = (values.Count == 1) && (width == 1) && ((selection.Bottom > selection.Top) || (selection.Right > selection.Left));
        var bottom = fill ? selection.Bottom : selection.Top + values.Count - 1;
        var right = fill ? selection.Right : selection.Left + width - 1;
        var cutRows = Math.Max(0, bottom - (Sheet.RowCount - 1));
        var cutColumns = Math.Max(0, right - (Sheet.ColumnCount - 1));
        var range = new CellRange(selection.Top, selection.Left, bottom - cutRows, right - cutColumns);

        var (pasteRows, pasteColumns) = (RowsOf(range), ColumnsOf(range));
        var pasting = new SheetPastingEventArgs(Sheet, range, values);
        await HandleAsync(Pasting, pasting).ConfigureAwait(true);
        if (pasting.Cancel || (Relocate(pasteRows, pasteColumns) is not { } moved))
        {
            return new PasteResult(selection.Top, selection.Left, selection.Bottom, selection.Right, null);
        }

        range = moved;

        // Cells beyond the end of a short row are left unchanged
        var (marked, rejected) = await InputCellsAsync(
            range,
            (r, c) =>
            {
                var cells = fill ? values[0] : values[r - range.Top];
                var index = fill ? 0 : c - range.Left;
                return index < cells.Length ? cells[index] : null;
            },
            CellChangeReason.Paste).ConfigureAwait(true);
        return new PasteResult(range.Top, range.Left, range.Bottom, range.Right, PasteMessage(marked, rejected, cutRows, cutColumns));
    }

    // Each cell is an optimistic commit: invalid input gets an error mark, or is rejected in Revert mode
    private async Task<(int Marked, int Rejected)> InputCellsAsync(CellRange range, Func<int, int, string?> textAt, CellChangeReason reason, CellRange? area = null)
    {
        var batch = new List<CellChange>();
        var (marked, rejected) = (0, 0);
        var generation = history.Generation;
        var (rows, columns) = (RowsOf(range), ColumnsOf(range));
        var edited = AreaOf(area ?? range);

        // Handlers can change the sheet while cells are entered, so every text is read first
        var texts = new string?[rows.Length * columns.Length];
        for (var r = 0; r < rows.Length; r++)
        {
            for (var c = 0; c < columns.Length; c++)
            {
                texts[(r * columns.Length) + c] = textAt(range.Top + r, range.Left + c);
            }
        }

        using (Sheet.BeginUpdate())
        {
            for (var r = 0; r < rows.Length; r++)
            {
                for (var c = 0; c < columns.Length; c++)
                {
                    var (sheetRow, sheetColumn) = (rows[r], columns[c]);
                    if ((texts[(r * columns.Length) + c] is not { } text) || !Sheet.Contains(sheetRow) || !Sheet.Contains(sheetColumn) || !IsEditable(sheetRow.Index, sheetColumn.Index))
                    {
                        continue;
                    }

                    var result = await ApplyAsync(sheetRow.Index, sheetColumn.Index, Draft(sheetRow.Index, sheetColumn.Index, text), true, reason, batch).ConfigureAwait(true);
                    if (!result.Ok && result.Stored)
                    {
                        marked++;
                    }
                    else if (!result.Ok)
                    {
                        rejected++;
                    }
                }
            }
        }

        if ((batch.Count > 0) && (generation == history.Generation))
        {
            history.Record(new CellsEdited(batch, edited));
        }

        return (marked, rejected);
    }

    private async Task ClearCellsAsync(CellRange range, CellChangeReason reason)
    {
        if ((Sheet.RowCount == 0) || (Sheet.ColumnCount == 0))
        {
            return;
        }

        range = ClipToSheet(range);
        var batch = new List<CellChange>();
        var generation = history.Generation;
        var (rows, columns) = (RowsOf(range), ColumnsOf(range));
        var edited = AreaOf(range);
        using (Sheet.BeginUpdate())
        {
            foreach (var sheetRow in rows)
            {
                foreach (var sheetColumn in columns)
                {
                    if (Sheet.Contains(sheetRow) && Sheet.Contains(sheetColumn) && IsEditable(sheetRow.Index, sheetColumn.Index))
                    {
                        await ApplyAsync(sheetRow.Index, sheetColumn.Index, CommitDraft.Clear, true, reason, batch).ConfigureAwait(true);
                    }
                }
            }
        }

        if ((batch.Count > 0) && (generation == history.Generation))
        {
            history.Record(new CellsEdited(batch, edited));
        }
    }

    // A handler can insert or delete rows and columns, so a range is found again by its rows and columns
    private CellRange? Relocate(SheetRow[] rows, SheetColumn[] columns) =>
        (Span(rows) is { } vertical) && (Span(columns) is { } horizontal) ? new CellRange(vertical.First, horizontal.First, vertical.Last, horizontal.Last) : null;

    private (int First, int Last)? Span(IReadOnlyList<SheetRow> rows)
    {
        if (rows.Count == 0)
        {
            return null;
        }

        for (var i = 0; i < rows.Count; i++)
        {
            if (!Sheet.Contains(rows[i]) || (rows[i].Index != rows[0].Index + i))
            {
                return null;
            }
        }

        return (rows[0].Index, rows[^1].Index);
    }

    private (int First, int Last)? Span(IReadOnlyList<SheetColumn> columns)
    {
        if (columns.Count == 0)
        {
            return null;
        }

        for (var i = 0; i < columns.Count; i++)
        {
            if (!Sheet.Contains(columns[i]) || (columns[i].Index != columns[0].Index + i))
            {
                return null;
            }
        }

        return (columns[0].Index, columns[^1].Index);
    }

    private SheetRow[] RowsOf(CellRange range)
    {
        var rows = new SheetRow[range.Bottom - range.Top + 1];
        for (var i = 0; i < rows.Length; i++)
        {
            rows[i] = Sheet.Rows[range.Top + i];
        }

        return rows;
    }

    private SheetColumn[] ColumnsOf(CellRange range)
    {
        var columns = new SheetColumn[range.Right - range.Left + 1];
        for (var i = 0; i < columns.Length; i++)
        {
            columns[i] = Sheet.Columns[range.Left + i];
        }

        return columns;
    }

    private string? PasteMessage(int marked, int rejected, int cutRows, int cutColumns)
    {
        var texts = Texts;
        var parts = new List<string>();
        if (marked > 0)
        {
            parts.Add(InputContext.Fill(texts.PasteMarked, marked));
        }

        if (rejected > 0)
        {
            parts.Add(InputContext.Fill(texts.PasteRejected, rejected));
        }

        if ((cutRows > 0) && (cutColumns > 0))
        {
            parts.Add(InputContext.Fill(texts.PasteTruncatedBoth, cutRows.ToString(CultureInfo.InvariantCulture), cutColumns.ToString(CultureInfo.InvariantCulture)));
        }
        else if (cutRows > 0)
        {
            parts.Add(InputContext.Fill(texts.PasteTruncatedRows, cutRows));
        }
        else if (cutColumns > 0)
        {
            parts.Add(InputContext.Fill(texts.PasteTruncatedColumns, cutColumns));
        }

        return parts.Count == 0 ? null : string.Join(texts.MessageSeparator, parts);
    }
}
