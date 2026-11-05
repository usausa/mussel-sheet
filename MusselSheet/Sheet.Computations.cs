namespace MusselSheet;

using MusselSheet.Internal;

public sealed partial class Sheet
{
    private readonly List<SheetComputation> computations = [];

    public SheetComputation Compute(string target, IReadOnlyList<string> observed, Func<CellComputeContext, object?> compute)
    {
        var address = CellAddress.Parse(target);
        return Register(new SheetComputation(this, CellRange.Cell(address), false, [.. observed.Select(x => Observation.Of(this, x))], compute));
    }

    public SheetComputation ComputeRows(string range, IReadOnlyList<string> observed, Func<CellComputeContext, object?> compute)
    {
        var target = CellRange.Parse(range);
        if ((target.Left != target.Right) || (target.Left >= ColumnCount) || (!target.IsEntireColumns && (target.Bottom >= RowCount)))
        {
            throw new ArgumentOutOfRangeException(nameof(range), "The range must be one column inside the sheet.");
        }

        return Register(new SheetComputation(this, target, true, [.. observed.Select(x => CellAddress.TryParseColumn(x, out _) ? Observation.SameRow(this, x) : Observation.Of(this, x))], compute));
    }

    internal IReadOnlyList<SheetComputation> Computations => computations;

    internal void Uncompute(SheetComputation computation) => computations.Remove(computation);

    // The read-only rule is already part of the design, so it is not added again
    internal SheetComputation Restore(CellRange target, bool perRow, IReadOnlyList<Observation> observations, Func<CellComputeContext, object?> compute) =>
        Register(new SheetComputation(this, target, perRow, observations, compute), false);

    // Does nothing inside BeginUpdate; the pending computations run when the outermost update ends
    internal void RunComputations()
    {
        if (updateDepth > 0)
        {
            return;
        }

        foreach (var computation in computations.ToArray())
        {
            computation.RunPending();
        }
    }

    private SheetComputation Register(SheetComputation computation, bool readOnly = true)
    {
        if (readOnly)
        {
            Configure(computation.Target, c => c.ReadOnly = true);
        }

        computations.Add(computation);
        computation.Refresh();
        return computation;
    }

    private void NotifyComputations(SheetChangedEventArgs e)
    {
        if (computations.Count == 0)
        {
            return;
        }

        foreach (var computation in computations.ToArray())
        {
            computation.OnSheetChanged(e);
        }

        RunComputations();
    }
}
