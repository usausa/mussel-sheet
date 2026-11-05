namespace MusselSheet;

// A merged range behaves as its top-left cell: value, settings and error mark all come from it
public sealed partial class Sheet
{
    private readonly List<CellRange> merges = [];

    private Dictionary<long, CellRange>? mergeIndex;

    public IReadOnlyList<CellRange> MergeAreas => merges;

    public void Merge(CellRange range)
    {
        if (!range.TryClip(RowCount, ColumnCount, out var clipped) || (clipped != range))
        {
            throw new ArgumentOutOfRangeException(nameof(range), "The range to merge must be inside the sheet.");
        }

        if ((range.Top == range.Bottom) && (range.Left == range.Right))
        {
            throw new ArgumentException("A single cell cannot be merged.", nameof(range));
        }

        if (merges.Any(x => x.Intersects(range) && !Encloses(range, x)))
        {
            throw new InvalidOperationException($"{range} partially overlaps another merged range.");
        }

        merges.RemoveAll(x => Encloses(range, x));
        for (var r = range.Top; r <= range.Bottom; r++)
        {
            var row = rowList[r];
            var left = r == range.Top ? range.Left + 1 : range.Left;
            Array.Clear(row.Cells, left, range.Right - left + 1);
            row.ContentVersion++;
        }

        merges.Add(range);
        mergeIndex = null;
        Raise(new SheetChangedEventArgs(SheetChangeKind.Merges, range));
    }

    public void Unmerge(CellRange range)
    {
        if (merges.RemoveAll(x => x.Intersects(range)) == 0)
        {
            return;
        }

        mergeIndex = null;
        Raise(new SheetChangedEventArgs(SheetChangeKind.Merges, range));
    }

    public CellRange? GetMergeArea(int row, int column)
    {
        CheckCell(row, column);
        return MergeIndex().TryGetValue(MergeKey(row, column), out var range) ? range : null;
    }

    internal bool IsCovered(int row, int column) =>
        MergeIndex().TryGetValue(MergeKey(row, column), out var range) && ((range.Top != row) || (range.Left != column));

    internal void ReplaceMerges(IReadOnlyList<CellRange> next)
    {
        if (ReferenceEquals(next, merges))
        {
            return;
        }

        merges.Clear();
        merges.AddRange(next);
        mergeIndex = null;
    }

    private static bool Encloses(CellRange outer, CellRange inner) =>
        (inner.Top >= outer.Top) && (inner.Bottom <= outer.Bottom) && (inner.Left >= outer.Left) && (inner.Right <= outer.Right);

    private static long MergeKey(int row, int column) => ((long)row << 32) | (uint)column;

    private Dictionary<long, CellRange> MergeIndex()
    {
        if (mergeIndex is { } index)
        {
            return index;
        }

        index = [];
        foreach (var range in merges)
        {
            for (var r = range.Top; r <= range.Bottom; r++)
            {
                for (var c = range.Left; c <= range.Right; c++)
                {
                    index[MergeKey(r, c)] = range;
                }
            }
        }

        mergeIndex = index;
        return index;
    }
}
