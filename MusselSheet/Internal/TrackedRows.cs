namespace MusselSheet.Internal;

// Rows are tracked by object, and removed rows stay remembered, so undo brings them back as they were. A region
// that grows like a detail also takes rows inserted inside it and directly above or below it.
internal sealed class TrackedRows
{
    private readonly List<SheetRow> rows;

    private readonly HashSet<SheetRow> set;

    private readonly bool region;

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
            if (set.Add(row))
            {
                rows.Add(row);
                joined?.Add(row);
            }
        }
    }
}
