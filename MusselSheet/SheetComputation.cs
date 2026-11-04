namespace MusselSheet;

using MusselSheet.Internal;

public sealed class SheetComputation : IDisposable
{
    private readonly Sheet sheet;

    private readonly TrackedRows rows;

    private readonly SheetColumn column;

    private readonly HashSet<SheetRow> dirty = [];

    private bool running;

    private bool disposed;

    internal SheetComputation(Sheet sheet, CellRange target, bool perRow, IReadOnlyList<Observation> observations, Func<CellComputeContext, object?> compute)
    {
        this.sheet = sheet;
        PerRow = perRow;
        Observations = observations;
        Function = compute;
        Target = target;
        rows = target.IsEntireColumns ? TrackedRows.All() : TrackedRows.Of(sheet, target.Top, Math.Min(target.Bottom, sheet.RowCount - 1), perRow);
        column = sheet.Columns[target.Left];
    }

    internal CellRange Target { get; }

    internal Func<CellComputeContext, object?> Function { get; }

    internal bool PerRow { get; }

    internal IReadOnlyList<Observation> Observations { get; }

    public void Refresh()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        MarkAll();
        sheet.RunComputations();
    }

    internal string CurrentTarget()
    {
        if (!sheet.Contains(column))
        {
            return Target.ToString();
        }

        if (rows.IsAll)
        {
            return CellRange.Columns(column.Index, column.Index).ToString();
        }

        return rows.Span(sheet) is { } span ? new CellRange(span.Top, column.Index, span.Bottom, column.Index).ToString() : Target.ToString();
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        sheet.Uncompute(this);
    }

    // Changes made while this computation is running (its own results and their dependents) are ignored, which
    // also stops computation loops.
    internal void OnSheetChanged(SheetChangedEventArgs e)
    {
        if (disposed || running)
        {
            return;
        }

        switch (e.Kind)
        {
            case SheetChangeKind.Values when e.Range is { } range:
                MarkChanged(range);
                break;
            case SheetChangeKind.RowsInserted:
                foreach (var observation in Observations)
                {
                    observation.OnRowsInserted(sheet, e.Index, e.Count);
                }

                rows.OnInserted(sheet, e.Index, e.Count, dirty);
                if (!PerRow)
                {
                    MarkAll();
                }

                break;
            case SheetChangeKind.RowsDeleted when !PerRow:
            case SheetChangeKind.ColumnsInserted or SheetChangeKind.ColumnsDeleted:
                MarkAll();
                break;
        }
    }

    internal void RunPending()
    {
        if (disposed || running || (dirty.Count == 0))
        {
            return;
        }

        running = true;
        try
        {
            var targets = dirty.Where(sheet.Contains).OrderBy(x => x.Index).ToArray();
            dirty.Clear();
            foreach (var row in targets)
            {
                Calculate(row);
            }
        }
        finally
        {
            dirty.Clear();
            running = false;
        }
    }

    private void MarkChanged(CellRange range)
    {
        // A change in a range observation, or in a cell a per-row computation reads besides its own row (for example a
        // rate in the header), recomputes every row.
        if (Observations.Any(x => !x.IsSameRow && x.Intersects(sheet, range)))
        {
            MarkAll();
            return;
        }

        if (!PerRow || !Observations.Any(x => x.IsSameRow && x.Intersects(sheet, range)))
        {
            return;
        }

        var top = Math.Max(range.Top, 0);
        var bottom = Math.Min(range.Bottom, sheet.RowCount - 1);
        for (var r = top; r <= bottom; r++)
        {
            var row = sheet.Rows[r];
            if (rows.Contains(sheet, row))
            {
                dirty.Add(row);
            }
        }
    }

    private void MarkAll()
    {
        foreach (var row in rows.Current(sheet))
        {
            dirty.Add(row);
        }
    }

    private void Calculate(SheetRow row)
    {
        if (!sheet.Contains(column))
        {
            return;
        }

        var (r, c) = (row.Index, column.Index);
        var value = Function(new CellComputeContext(sheet, new CellAddress(r, c), Observations, PerRow ? row : null));
        if (!Equals(sheet.GetValue(r, c), value))
        {
            sheet.SetValue(r, c, value);
        }
    }
}
