namespace MusselSheet.Internal;

internal sealed class AxisLayout
{
    private int[] offsets = [0];

    public int Count => offsets.Length - 1;

    public int Total => offsets[^1];

    public void Reset(int count, Func<int, int> sizeOf)
    {
        var values = new int[count + 1];
        for (var i = 0; i < count; i++)
        {
            values[i + 1] = values[i] + sizeOf(i);
        }

        offsets = values;
    }

    public int Start(int index) => offsets[index];

    public int End(int index) => offsets[index + 1];

    public int IndexAt(int position)
    {
        var low = 0;
        var high = Math.Max(Count - 1, 0);
        while (low < high)
        {
            var mid = (low + high + 1) / 2;
            if (offsets[mid] <= position)
            {
                low = mid;
            }
            else
            {
                high = mid - 1;
            }
        }

        return low;
    }

    // Returns (0, -1) when there are no items
    public (int First, int Last) GetRange(int start, int length, int overscan)
    {
        if (Count == 0)
        {
            return (0, -1);
        }

        var first = IndexAt(start);
        var last = IndexAt(start + Math.Max(length, 1) - 1);
        return (Math.Max(0, first - overscan), Math.Min(Count - 1, last + overscan));
    }
}
