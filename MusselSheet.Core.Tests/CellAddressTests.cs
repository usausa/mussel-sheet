namespace MusselSheet;

public sealed class CellAddressTests
{
    [Theory]
    [InlineData(0, "A")]
    [InlineData(25, "Z")]
    [InlineData(26, "AA")]
    [InlineData(51, "AZ")]
    [InlineData(52, "BA")]
    [InlineData(701, "ZZ")]
    [InlineData(702, "AAA")]
    [InlineData(16383, "XFD")]
    public void ColumnNameAndTryParseColumnAreInverse(int column, string name)
    {
        Assert.Equal(name, CellAddress.ColumnName(column));
        Assert.True(CellAddress.TryParseColumn(name, out var parsed));
        Assert.Equal(column, parsed);
    }

    [Theory]
    [InlineData("A1", 0, 0)]
    [InlineData("B3", 2, 1)]
    [InlineData("z10", 9, 25)]
    [InlineData("AA100", 99, 26)]
    [InlineData(" C2 ", 1, 2)]
    [InlineData("XFD1048576", 1048575, 16383)]
    public void ParseReadsA1Text(string text, int row, int column)
    {
        Assert.Equal(new CellAddress(row, column), CellAddress.Parse(text));
    }

    [Theory]
    [InlineData("")]
    [InlineData("A")]
    [InlineData("1")]
    [InlineData("A0")]
    [InlineData("1A")]
    [InlineData("A-1")]
    [InlineData("A1B")]
    [InlineData("XFE1")]
    [InlineData("Ａ1")]
    public void InvalidTextIsRejected(string text)
    {
        Assert.False(CellAddress.TryParse(text, out _));
        Assert.Throws<FormatException>(() => CellAddress.Parse(text));
    }

    [Fact]
    public void NullIsRejected()
    {
        Assert.False(CellAddress.TryParse(null, out _));
    }

    [Fact]
    public void ToStringWritesA1Text()
    {
        Assert.Equal("A1", new CellAddress(0, 0).ToString());
        Assert.Equal("AB12", new CellAddress(11, 27).ToString());
    }
}
