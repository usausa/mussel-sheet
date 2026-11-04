namespace MusselSheet;

public sealed class CellRangeTests
{
    [Theory]
    [InlineData("A1", 0, 0, 0, 0)]
    [InlineData("A1:C3", 0, 0, 2, 2)]
    [InlineData("C3:A1", 0, 0, 2, 2)]
    [InlineData("C1:A3", 0, 0, 2, 2)]
    [InlineData(" B2 : D5 ", 1, 1, 4, 3)]
    public void ParseReadsRectangles(string text, int top, int left, int bottom, int right)
    {
        Assert.Equal(new CellRange(top, left, bottom, right), CellRange.Parse(text));
    }

    [Fact]
    public void ParseReadsEntireColumns()
    {
        var range = CellRange.Parse("D:B");

        Assert.Equal(CellRange.Columns(1, 3), range);
        Assert.True(range.IsEntireColumns);
        Assert.False(range.IsEntireRows);
    }

    [Fact]
    public void ParseReadsEntireRows()
    {
        var range = CellRange.Parse("4:2");

        Assert.Equal(CellRange.Rows(1, 3), range);
        Assert.True(range.IsEntireRows);
        Assert.False(range.IsEntireColumns);
    }

    [Theory]
    [InlineData("")]
    [InlineData(":")]
    [InlineData("A1:")]
    [InlineData("A:1")]
    [InlineData("A1:B")]
    [InlineData("0:3")]
    [InlineData("A1:C3:D4")]
    public void InvalidTextIsRejected(string text)
    {
        Assert.False(CellRange.TryParse(text, out _));
        Assert.Throws<FormatException>(() => CellRange.Parse(text));
    }

    [Theory]
    [InlineData("B2")]
    [InlineData("A1:C3")]
    [InlineData("B:D")]
    [InlineData("1:3")]
    [InlineData("2:5")]
    public void ToStringRoundTrips(string text)
    {
        Assert.Equal(text, CellRange.Parse(text).ToString());
    }

    [Fact]
    public void AllIsWrittenAsAsterisk()
    {
        Assert.Equal("*", CellRange.All.ToString());
        Assert.True(CellRange.All.IsEntireRows);
        Assert.True(CellRange.All.IsEntireColumns);
    }

    [Fact]
    public void ContainsIncludesBothEdges()
    {
        var range = CellRange.Parse("B2:D5");

        Assert.True(range.Contains(1, 1));
        Assert.True(range.Contains(new CellAddress(4, 3)));
        Assert.False(range.Contains(0, 1));
        Assert.False(range.Contains(1, 4));
        Assert.True(CellRange.Columns(1, 1).Contains(100000, 1));
    }

    [Fact]
    public void IntersectsDetectsOverlap()
    {
        var range = CellRange.Parse("B2:D5");

        Assert.True(range.Intersects(CellRange.Parse("D5:F8")));
        Assert.False(range.Intersects(CellRange.Parse("E1:F8")));
        Assert.True(range.Intersects(CellRange.Rows(4, 4)));
        Assert.True(range.Intersects(CellRange.All));
    }

    [Fact]
    public void TryClipFitsTheRangeIntoTheSheet()
    {
        Assert.True(CellRange.Columns(1, 2).TryClip(10, 5, out var clipped));
        Assert.Equal(new CellRange(0, 1, 9, 2), clipped);
        Assert.True(CellRange.All.TryClip(3, 4, out clipped));
        Assert.Equal(new CellRange(0, 0, 2, 3), clipped);
        Assert.False(CellRange.Parse("F1").TryClip(10, 5, out _));
    }

    [Fact]
    public void ConstructorRejectsReversedOrNegativeCorners()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new CellRange(2, 0, 1, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new CellRange(0, 2, 0, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new CellRange(-1, 0, 1, 0));
    }
}
