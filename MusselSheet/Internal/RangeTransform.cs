namespace MusselSheet.Internal;

internal enum Axis
{
    Row,

    Column,
}

internal static class RangeTransform
{
    public static List<SettingsRule> Insert(IReadOnlyList<SettingsRule> rules, Axis axis, int index, int count, bool inherit, int? source)
    {
        var result = new List<SettingsRule>(rules.Count);
        foreach (var rule in rules)
        {
            var (start, end) = GetSpan(rule.Range, axis);
            var unbounded = end == CellRange.End;
            if (unbounded && (start == 0))
            {
                result.Add(rule);
            }
            else if (start >= index)
            {
                var moved = rule with { Range = WithSpan(rule.Range, axis, start + count, unbounded ? CellRange.End : end + count) };
                result.Add(moved);
                AddInherited(result, moved, axis, index, count, source);
            }
            else if (unbounded || (end >= index))
            {
                AddSpanning(result, rule, axis, index, count, inherit);
            }
            else
            {
                result.Add(rule);
                AddInherited(result, rule, axis, index, count, source);
            }
        }

        return result;
    }

    public static List<SettingsRule> Remove(IReadOnlyList<SettingsRule> rules, Axis axis, int index, int count)
    {
        var last = index + count - 1;
        var result = new List<SettingsRule>(rules.Count);
        foreach (var rule in rules)
        {
            var (start, end) = GetSpan(rule.Range, axis);
            var unbounded = end == CellRange.End;
            if (!unbounded && (end < index))
            {
                result.Add(rule);
            }
            else if (start > last)
            {
                result.Add(rule with { Range = WithSpan(rule.Range, axis, start - count, unbounded ? CellRange.End : end - count) });
            }
            else if (unbounded || (start < index) || (end > last))
            {
                var newStart = Math.Min(start, index);
                var newEnd = unbounded ? CellRange.End : (end > last ? end - count : index - 1);
                result.Add(rule with { Range = WithSpan(rule.Range, axis, newStart, newEnd) });
            }
        }

        return result;
    }

    // Inserting inside a merge (after its first row or column) grows it; inserting at or before the first shifts it
    public static List<CellRange> InsertMerges(IReadOnlyList<CellRange> merges, Axis axis, int index, int count)
    {
        var result = new List<CellRange>(merges.Count);
        foreach (var range in merges)
        {
            var (start, end) = GetSpan(range, axis);
            if (start >= index)
            {
                result.Add(WithSpan(range, axis, start + count, end + count));
            }
            else if (end >= index)
            {
                result.Add(WithSpan(range, axis, start, end + count));
            }
            else
            {
                result.Add(range);
            }
        }

        return result;
    }

    public static List<CellRange> RemoveMerges(IReadOnlyList<CellRange> merges, Axis axis, int index, int count)
    {
        var last = index + count - 1;
        var result = new List<CellRange>(merges.Count);
        foreach (var range in merges)
        {
            var (start, end) = GetSpan(range, axis);
            var moved = range;
            if (start > last)
            {
                moved = WithSpan(range, axis, start - count, end - count);
            }
            else if (end >= index)
            {
                var newStart = Math.Min(start, index);
                var newEnd = end > last ? end - count : index - 1;
                if (newEnd < newStart)
                {
                    continue;
                }

                moved = WithSpan(range, axis, newStart, newEnd);
            }

            if ((moved.Top != moved.Bottom) || (moved.Left != moved.Right))
            {
                result.Add(moved);
            }
        }

        return result;
    }

    private static void AddSpanning(List<SettingsRule> result, SettingsRule rule, Axis axis, int index, int count, bool inherit)
    {
        var (start, end) = GetSpan(rule.Range, axis);
        if (inherit || (end == CellRange.End))
        {
            result.Add(rule with { Range = WithSpan(rule.Range, axis, start, end == CellRange.End ? CellRange.End : end + count) });
            return;
        }

        result.Add(rule with { Range = WithSpan(rule.Range, axis, start, index - 1) });
        result.Add(rule with { Range = WithSpan(rule.Range, axis, index + count, end + count) });
    }

    // The copy goes right after the original rule, which preserves the priority order
    private static void AddInherited(List<SettingsRule> result, SettingsRule rule, Axis axis, int index, int count, int? source)
    {
        if (source is not { } from)
        {
            return;
        }

        var (start, end) = GetSpan(rule.Range, axis);
        if ((from < start) || (from > end))
        {
            return;
        }

        result.Add(rule with { Range = WithSpan(rule.Range, axis, index, index + count - 1) });
    }

    private static (int Start, int End) GetSpan(CellRange range, Axis axis) =>
        axis == Axis.Row ? (range.Top, range.Bottom) : (range.Left, range.Right);

    private static CellRange WithSpan(CellRange range, Axis axis, int start, int end) =>
        axis == Axis.Row ? new CellRange(start, range.Left, end, range.Right) : new CellRange(range.Top, start, range.Bottom, end);
}
