namespace MusselSheet;

public sealed class BorderEdgeTests
{
    [Fact]
    public void LaterLeftBorderOfTheRightCellWins()
    {
        var sheet = new Sheet(1, 2);
        sheet.Configure(CellRange.Parse("A1"), c => c.Style.Borders.Right = CellBorder.Thin);
        sheet.Configure(CellRange.Parse("B1"), c => c.Style.Borders.Left = CellBorder.Thick);

        Assert.Equal(CellBorder.Thick, sheet.GetEdges(0, 0).Right);
    }

    [Fact]
    public void LaterRightBorderOfTheLeftCellWins()
    {
        var sheet = new Sheet(1, 2);
        sheet.Configure(CellRange.Parse("B1"), c => c.Style.Borders.Left = CellBorder.Thick);
        sheet.Configure(CellRange.Parse("A1"), c => c.Style.Borders.Right = CellBorder.Dashed);

        Assert.Equal(CellBorder.Dashed, sheet.GetEdges(0, 0).Right);
    }

    [Fact]
    public void TopBorderOfTheCellBelowBecomesTheBottomEdge()
    {
        var sheet = new Sheet(2, 1);
        sheet.Configure(CellRange.Parse("A2"), c => c.Style.Borders.Top = CellBorder.DoubleLine);

        Assert.Equal(CellBorder.DoubleLine, sheet.GetEdges(0, 0).Bottom);
        Assert.Null(sheet.GetEdges(1, 0).Bottom);
    }

    [Fact]
    public void OutlineAfterAllBordersMakesOnlyTheOuterEdgesThicker()
    {
        var sheet = new Sheet(4, 4);
        sheet.Range("B2:C3").Configure(c => c.Style.Borders = CellBorders.All(CellBorder.Thin));

        sheet.Range("B2:C3").BorderAround(CellBorder.Medium);

        Assert.Equal(CellBorder.Medium, sheet.GetEdges(1, 0).Right);
        Assert.Equal(CellBorder.Thin, sheet.GetEdges(1, 1).Right);
        Assert.Equal(CellBorder.Medium, sheet.GetEdges(1, 2).Right);
        Assert.Equal(CellBorder.Medium, sheet.GetEdges(0, 1).Bottom);
        Assert.Equal(CellBorder.Thin, sheet.GetEdges(1, 1).Bottom);
        Assert.Equal(CellBorder.Medium, sheet.GetEdges(2, 1).Bottom);
        Assert.Null(sheet.GetEdges(0, 0).Right);
    }

    [Fact]
    public void EdgeOfTheSheetIgnoresRulesBeyondIt()
    {
        var sheet = new Sheet(2, 2);
        sheet.Configure(CellRange.All, c => c.Style.Borders.Left = CellBorder.Thick);

        Assert.Null(sheet.GetEdges(0, 1).Right);
        Assert.Equal(CellBorder.Thick, sheet.GetEdges(0, 0).Right);
    }

    [Fact]
    public void LaterNoneRemovesTheLine()
    {
        var sheet = new Sheet(1, 2);
        sheet.Configure(CellRange.All, c => c.Style.Borders = CellBorders.All(CellBorder.Thin));
        sheet.Configure(CellRange.Parse("B1"), c => c.Style.Borders.Left = CellBorder.None);

        Assert.Equal(CellBorder.None, sheet.GetEdges(0, 0).Right);
    }

    [Fact]
    public void EdgesFollowInsertedColumns()
    {
        var sheet = new Sheet(1, 2);
        sheet.Configure(CellRange.Parse("B1"), c => c.Style.Borders.Left = CellBorder.Thick);
        Assert.Equal(CellBorder.Thick, sheet.GetEdges(0, 0).Right);

        sheet.InsertColumns(1, 1, inheritSettings: false);

        Assert.Null(sheet.GetEdges(0, 0).Right);
        Assert.Equal(CellBorder.Thick, sheet.GetEdges(0, 1).Right);
    }
}
