namespace MusselSheet.Internal;

// Follows inserts and deletes; Name stays the registered address, which the compute function reads by
internal sealed class Observation
{
    private readonly TrackedRows? rows;

    private readonly IReadOnlyList<SheetColumn>? columns;

    private int cachedVersion = -1;

    private int[] cachedColumns = [];

    private SheetRow[] cachedRows = [];

    private (int Top, int Bottom)? cachedSpan;

    private Observation(string name, TrackedRows? rows, IReadOnlyList<SheetColumn>? columns)
    {
        Name = name;
        this.rows = rows;
        this.columns = columns;
    }

    public string Name { get; }

    public bool IsSameRow => rows is null;

    public static Observation Of(Sheet sheet, string text, string? name = null)
    {
        name ??= text;
        if (CellAddress.TryParse(text, out var address) && (address.Row < sheet.RowCount) && (address.Column < sheet.ColumnCount))
        {
            return new Observation(name, TrackedRows.Of(sheet, address.Row, address.Row, false), [sheet.Columns[address.Column]]);
        }

        if (!CellRange.TryParse(text, out var range) || !range.TryClip(sheet.RowCount, sheet.ColumnCount, out var clipped))
        {
            throw new ArgumentException($"'{text}' is not a cell or a range inside the sheet.", nameof(text));
        }

        var observedRows = range.IsEntireColumns ? TrackedRows.All() : TrackedRows.Of(sheet, clipped.Top, clipped.Bottom, clipped.Bottom > clipped.Top);
        var observedColumns = range.IsEntireRows ? null : sheet.Columns.Skip(clipped.Left).Take(clipped.Right - clipped.Left + 1).ToArray();
        return new Observation(name, observedRows, observedColumns);
    }

    public static Observation SameRow(Sheet sheet, string column, string? name = null)
    {
        if (!CellAddress.TryParseColumn(column, out var index) || (index >= sheet.ColumnCount))
        {
            throw new ArgumentException($"'{column}' is not a column inside the sheet.", nameof(column));
        }

        return new Observation(name ?? column, null, [sheet.Columns[index]]);
    }

    // Null when the observed cells were deleted, or a column inserted between them would join the saved range
    public string? CurrentText(Sheet sheet)
    {
        Refresh(sheet);
        var current = cachedColumns;
        if ((current.Length == 0) || (current[^1] - current[0] + 1 != current.Length))
        {
            return null;
        }

        if (rows is null)
        {
            return CellAddress.ColumnName(current[0]);
        }

        if (rows.IsAll)
        {
            return CellRange.Columns(current[0], current[^1]).ToString();
        }

        if (rows.Span(sheet) is not { } span)
        {
            return null;
        }

        return columns is null ? CellRange.Rows(span.Top, span.Bottom).ToString() : new CellRange(span.Top, current[0], span.Bottom, current[^1]).ToString();
    }

    public IEnumerable<CellAddress> Cells(Sheet sheet, SheetRow? row)
    {
        Refresh(sheet);
        IEnumerable<SheetRow> lines = rows is null ? ((row is not null) && sheet.Contains(row) ? [row] : []) : rows.IsAll ? sheet.Rows : cachedRows;
        foreach (var line in lines)
        {
            foreach (var column in cachedColumns)
            {
                yield return new CellAddress(line.Index, column);
            }
        }
    }

    public bool Intersects(Sheet sheet, CellRange range)
    {
        Refresh(sheet);
        var hit = false;
        foreach (var column in cachedColumns)
        {
            if ((column >= range.Left) && (column <= range.Right))
            {
                hit = true;
                break;
            }
        }

        return hit && ((rows is null) || ((cachedSpan is { } span) && (span.Top <= range.Bottom) && (span.Bottom >= range.Top)));
    }

    // The cache may have been made during the insert, before the rows joined
    public void OnRowsInserted(Sheet sheet, int index, int count)
    {
        rows?.OnInserted(sheet, index, count, null);
        cachedVersion = -1;
    }

    public void OnRowsRemoved(Sheet sheet)
    {
        rows?.OnRemoved(sheet);
        cachedRows = [];
        cachedVersion = -1;
    }

    // Built once per structure version, as every computed row reads the same cells
    private void Refresh(Sheet sheet)
    {
        if (cachedVersion == sheet.StructureVersion)
        {
            return;
        }

        cachedColumns = columns is null ? [.. Enumerable.Range(0, sheet.ColumnCount)] : [.. columns.Where(sheet.Contains).Select(static x => x.Index).Order()];
        cachedRows = (rows is null) || rows.IsAll ? [] : [.. rows.Current(sheet)];
        cachedSpan = rows?.Span(sheet);
        cachedVersion = sheet.StructureVersion;
    }
}
