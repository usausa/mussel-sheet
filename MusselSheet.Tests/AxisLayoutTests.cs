namespace MusselSheet;

using MusselSheet.Internal;

public sealed class AxisLayoutTests
{
    [Fact]
    public void OffsetsAreRunningTotals()
    {
        var layout = Create(10, 20, 30);

        Assert.Equal(3, layout.Count);
        Assert.Equal(60, layout.Total);
        Assert.Equal(10, layout.Start(1));
        Assert.Equal(30, layout.End(1));
    }

    [Theory]
    [InlineData(-5, 0)]
    [InlineData(0, 0)]
    [InlineData(9, 0)]
    [InlineData(10, 1)]
    [InlineData(29, 1)]
    [InlineData(30, 2)]
    [InlineData(1000, 2)]
    public void IndexAtFindsTheContainingItem(int position, int expected)
    {
        Assert.Equal(expected, Create(10, 20, 30).IndexAt(position));
    }

    [Fact]
    public void GetRangeAddsOverscanWithinBounds()
    {
        var layout = Create([.. Enumerable.Repeat(24, 100)]);

        Assert.Equal((10, 19), layout.GetRange(240, 240, 0));
        Assert.Equal((7, 22), layout.GetRange(240, 240, 3));
        Assert.Equal((0, 12), layout.GetRange(0, 240, 3));
        Assert.Equal((96, 99), layout.GetRange(2400, 240, 3));
    }

    [Fact]
    public void GetRangeUsesEachSize()
    {
        var layout = Create(24, 24, 100, 24, 24);

        Assert.Equal((2, 2), layout.GetRange(50, 90, 0));
        Assert.Equal((1, 3), layout.GetRange(40, 120, 0));
    }

    [Fact]
    public void EmptyLayoutHasNoRange()
    {
        var layout = Create();

        Assert.Equal(0, layout.Total);
        Assert.Equal((0, -1), layout.GetRange(0, 100, 5));
    }

    private static AxisLayout Create(params int[] sizes)
    {
        var layout = new AxisLayout();
        layout.Reset(sizes.Length, i => sizes[i]);
        return layout;
    }
}
