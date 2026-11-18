namespace MusselSheet.Internal;

using System.Runtime.CompilerServices;

// Tracks rows by object and remembers removed ones weakly for undo; a growing region also takes rows inserted next to it
internal sealed class TrackedRows
{
    private static readonly object Removed = new();

    private readonly List<SheetRow> rows;

    private readonly HashSet<SheetRow> set;

    private readonly bool region;

    private ConditionalWeakTable<SheetRow, object>? removed;

    private TrackedRows(List<SheetRow> rows, bool region, bool all)
    {
        this.rows = rows;
        set = [.. rows];
        this.region = region;
        IsAll = all;
    }

    public static TrackedRows All() => new([], false, true);

    public static TrackedRows Of(Sheet sheet, int top, int bottom, bool region)
    {
        var rows = new List<SheetRow>(bottom - top + 1);
        for (var r = top; r <= bottom; r++)
        {
            rows.Add(sheet.Rows[r]);
        }

        return new TrackedRows(rows, region, false);
    }

    public IEnumerable<SheetRow> Current(Sheet sheet) =>
        IsAll ? sheet.Rows : rows.Where(sheet.Contains).OrderBy(x => x.Index);

    public bool IsAll { get; }

    public bool Contains(Sheet sheet, SheetRow row) => sheet.Contains(row) && (IsAll || set.Contains(row));

    // Null unless the rows still in the sheet are one run without a gap
    public (int Top, int Bottom)? Run(Sheet sheet)
    {
        if (IsAll || (Span(sheet) is not { } span))
        {
            return Span(sheet);
        }

        var count = 0;
        foreach (var row in rows)
        {
            if (sheet.Contains(row))
            {
                count++;
            }
        }

        return span.Bottom - span.Top + 1 == count ? span : null;
    }

    public (int Top, int Bottom)? Span(Sheet sheet)
    {
        if (IsAll)
        {
            return sheet.RowCount > 0 ? (0, sheet.RowCount - 1) : null;
        }

        var (top, bottom) = (int.MaxValue, -1);
        foreach (var row in rows)
        {
            if (sheet.Contains(row))
            {
                top = Math.Min(top, row.Index);
                bottom = Math.Max(bottom, row.Index);
            }
        }

        return bottom < 0 ? null : (top, bottom);
    }

    public void OnInserted(Sheet sheet, int index, int count, ICollection<SheetRow>? joined)
    {
        if (IsAll)
        {
            for (var r = index; r < index + count; r++)
            {
                joined?.Add(sheet.Rows[r]);
            }

            return;
        }

        PutBack(sheet, index, count, joined);
        if (!region || (Span(sheet) is not { } span))
        {
            return;
        }

        var above = index > 0 ? sheet.Rows[index - 1] : null;
        var below = index + count < sheet.RowCount ? sheet.Rows[index + count] : null;
        if (((above is null) || !Contains(sheet, above)) && ((below is null) || (below.Index != span.Top) || !Contains(sheet, below)))
        {
            return;
        }

        for (var r = index; r < index + count; r++)
        {
            var row = sheet.Rows[r];
            if (!row.WasRemoved && set.Add(row))
            {
                rows.Add(row);
                joined?.Add(row);
            }
        }
    }

    // Only the undo history keeps a removed row alive, so the rows it lets go of are not held here
    public void OnRemoved(Sheet sheet)
    {
        if (IsAll)
        {
            return;
        }

        var kept = 0;
        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            if (sheet.Contains(row))
            {
                rows[kept++] = row;
            }
            else
            {
                set.Remove(row);
                (removed ??= new()).AddOrUpdate(row, Removed);
            }
        }

        rows.RemoveRange(kept, rows.Count - kept);
    }

    private void PutBack(Sheet sheet, int index, int count, ICollection<SheetRow>? joined)
    {
        if (removed is null)
        {
            return;
        }

        for (var r = index; r < index + count; r++)
        {
            var row = sheet.Rows[r];
            if (removed.Remove(row) && set.Add(row))
            {
                rows.Add(row);
                joined?.Add(row);
            }
        }
    }
}
