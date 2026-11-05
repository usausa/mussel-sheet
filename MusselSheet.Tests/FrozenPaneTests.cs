namespace MusselSheet;

public sealed class FrozenPaneTests : BunitContext
{
    public FrozenPaneTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public async Task FrozenRowsAreDrawnAboveTheVirtualizedRows()
    {
        var sheet = new Sheet(1000, 3);

        var cut = Render<MusselSheetView>(p => p
            .Add(x => x.Sheet, sheet)
            .Add(x => x.Height, "264px")
            .Add(x => x.OverscanCount, 2)
            .Add(x => x.FrozenRows, 2));

        string[] frozen = ["0", "1"];
        Assert.Equal(frozen, cut.FindAll(".mussel-sheet-top .mussel-sheet-row").Select(x => x.GetAttribute("data-r")));
        Assert.Contains("mussel-sheet-frozen-rows", cut.Find(".mussel-sheet").ClassList);
        Assert.Equal("2", BodyRows(cut)[0].GetAttribute("data-r"));

        await cut.InvokeAsync(() => cut.Instance.OnViewportChanged(24 * 500, 264, 0, 1000));

        Assert.Equal(frozen, cut.FindAll(".mussel-sheet-top .mussel-sheet-row").Select(x => x.GetAttribute("data-r")));
        var body = BodyRows(cut);
        Assert.Equal("500", body[0].GetAttribute("data-r"));
        Assert.Equal("511", body[^1].GetAttribute("data-r"));
        Assert.Equal("height:11952px", cut.FindAll(".mussel-sheet-spacer")[0].GetAttribute("style"));
    }

    [Fact]
    public void FrozenColumnsStickToTheRightOfTheRowHeader()
    {
        var sheet = new Sheet(2, 4);
        sheet.Columns[0].Width = 80;

        var cut = Render<MusselSheetView>(p => p.Add(x => x.Sheet, sheet).Add(x => x.FrozenColumns, 2));

        var row = cut.Find(".mussel-sheet-row[data-r='0']");
        string[] direct = ["0", "1"];
        string[] rest = ["2", "3"];
        Assert.Equal(direct, row.Children.Where(x => x.ClassList.Contains("mussel-sheet-cell")).Select(x => x.GetAttribute("data-c")));
        Assert.Equal(rest, row.QuerySelectorAll(".mussel-sheet-rest > .mussel-sheet-cell").Select(x => x.GetAttribute("data-c")));

        var css = cut.Find("style").TextContent;
        Assert.Contains(".mussel-sheet-colhead{grid-template-columns: 40px 80px 100px 200px}", css, StringComparison.Ordinal);
        Assert.Contains(".mussel-sheet-rest{grid-template-columns: 100px 100px}", css, StringComparison.Ordinal);
        Assert.Contains("[data-c=\"0\"]{position:sticky;left:40px;z-index:2}", css, StringComparison.Ordinal);
        Assert.Contains("[data-c=\"1\"]{position:sticky;left:120px;z-index:2}", css, StringComparison.Ordinal);
        Assert.Contains("[data-c=\"1\"]{box-shadow:1px 0 0 var(--mussel-sheet-freeze-color)}", css, StringComparison.Ordinal);
        Assert.DoesNotContain("[data-c=\"2\"]{position:sticky", css, StringComparison.Ordinal);
    }

    [Fact]
    public void WithoutFrozenColumnsAllCellsAreInTheRest()
    {
        var sheet = new Sheet(1, 3);

        var cut = Render<MusselSheetView>(p => p.Add(x => x.Sheet, sheet));

        Assert.Equal(3, cut.FindAll(".mussel-sheet-row[data-r='0'] > .mussel-sheet-rest > .mussel-sheet-cell").Count);
        Assert.Contains(".mussel-sheet-colhead{grid-template-columns: 40px 300px}", cut.Find("style").TextContent, StringComparison.Ordinal);
        Assert.DoesNotContain("mussel-sheet-frozen-rows", cut.Find(".mussel-sheet").ClassList);
        Assert.DoesNotContain("position:sticky", cut.Find("style").TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public void FrozenCountsAreLimitedToTheSheet()
    {
        var sheet = new Sheet(3, 2);

        var cut = Render<MusselSheetView>(p => p
            .Add(x => x.Sheet, sheet)
            .Add(x => x.FrozenRows, 10)
            .Add(x => x.FrozenColumns, 10));

        Assert.Equal(3, cut.FindAll(".mussel-sheet-top .mussel-sheet-row").Count);
        Assert.Empty(BodyRows(cut));
        Assert.Empty(cut.FindAll(".mussel-sheet-spacer"));
        Assert.Empty(cut.FindAll(".mussel-sheet-rest > .mussel-sheet-cell"));
        Assert.Contains(".mussel-sheet-rest{grid-template-columns: none}", cut.Find("style").TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SheetFrozenCountsAreUsedUnlessTheParameterIsGiven()
    {
        var sheet = new Sheet(10, 3) { FrozenRows = 2 };

        var cut = Render<MusselSheetView>(p => p.Add(x => x.Sheet, sheet));
        Assert.Equal(["0", "1"], FrozenRows(cut));

        await cut.InvokeAsync(() =>
        {
            sheet.FrozenRows = 1;
            sheet.FrozenColumns = 1;
        });
        Assert.Equal(["0"], FrozenRows(cut));
        Assert.Equal(["0"], cut.Find(".mussel-sheet-row[data-r='5']").Children.Where(x => x.ClassList.Contains("mussel-sheet-cell")).Select(x => x.GetAttribute("data-c")));

        cut.Render(p => p.Add(x => x.FrozenRows, 3));
        Assert.Equal(["0", "1", "2"], FrozenRows(cut));
    }

    private static IEnumerable<string?> FrozenRows(IRenderedComponent<MusselSheetView> cut) =>
        cut.FindAll(".mussel-sheet-top .mussel-sheet-row").Select(x => x.GetAttribute("data-r"));

    private static List<AngleSharp.Dom.IElement> BodyRows(IRenderedComponent<MusselSheetView> cut) =>
        [.. cut.FindAll(".mussel-sheet-content > .mussel-sheet-row")];
}
