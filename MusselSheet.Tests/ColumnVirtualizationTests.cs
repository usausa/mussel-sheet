namespace MusselSheet;

public sealed class ColumnVirtualizationTests : BunitContext
{
    public ColumnVirtualizationTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public async Task FixedWidthRendersOnlyVisibleColumns()
    {
        var sheet = new Sheet(3, 100);

        var cut = Render<MusselSheetView>(p => p
            .Add(x => x.Sheet, sheet)
            .Add(x => x.Width, "500px")
            .Add(x => x.OverscanColumnCount, 2));

        // 460px (500px minus the 40px row header) reaches 5 columns of 100px, plus 2 overscan columns on the right
        Assert.Equal(Range(0, 6), Columns(cut, ".mussel-sheet-row[data-r='0'] .mussel-sheet-cell"));
        Assert.Equal(Range(0, 6), Columns(cut, ".mussel-sheet-colhead-cell"));
        Assert.Contains(".mussel-sheet-rest{padding-left:0px}", Css(cut), StringComparison.Ordinal);

        await cut.InvokeAsync(() => cut.Instance.OnViewportChanged(0, 300, 5000, 500));

        Assert.Equal(Range(48, 56), Columns(cut, ".mussel-sheet-row[data-r='2'] .mussel-sheet-cell"));
        Assert.Equal(Range(48, 56), Columns(cut, ".mussel-sheet-colhead-cell"));
        Assert.Contains(".mussel-sheet-rest{padding-left:4800px}", Css(cut), StringComparison.Ordinal);
        Assert.Contains(".mussel-sheet-rest{grid-template-columns: 100px 100px 100px 100px 100px 100px 100px 100px 100px}", Css(cut), StringComparison.Ordinal);

        Assert.Equal("50", cut.Find(".mussel-sheet-row[data-r='2'] .mussel-sheet-cell").GetAttribute("aria-colindex"));
        Assert.Equal("101", cut.Find(".mussel-sheet-content").GetAttribute("aria-colcount"));
    }

    [Fact]
    public async Task FrozenColumnsAreAlwaysRendered()
    {
        var sheet = new Sheet(2, 100);

        var cut = Render<MusselSheetView>(p => p
            .Add(x => x.Sheet, sheet)
            .Add(x => x.Width, "500px")
            .Add(x => x.OverscanColumnCount, 0)
            .Add(x => x.FrozenColumns, 2));

        await cut.InvokeAsync(() => cut.Instance.OnViewportChanged(0, 300, 3000, 500));

        Assert.Equal([0, 1, .. Range(32, 34)], Columns(cut, ".mussel-sheet-row[data-r='0'] .mussel-sheet-cell"));
        Assert.Contains(".mussel-sheet-rest{padding-left:3000px}", Css(cut), StringComparison.Ordinal);
    }

    [Fact]
    public void AutoWidthRendersAllColumns()
    {
        var sheet = new Sheet(1, 30);

        var cut = Render<MusselSheetView>(p => p.Add(x => x.Sheet, sheet).Add(x => x.Height, "300px"));

        Assert.Equal(Range(0, 29), Columns(cut, ".mussel-sheet-row[data-r='0'] .mussel-sheet-cell"));
    }

    private static int[] Range(int first, int last) => [.. Enumerable.Range(first, last - first + 1)];

    private static int[] Columns(IRenderedComponent<MusselSheetView> cut, string selector) =>
        [.. cut.FindAll(selector).Select(x => int.Parse(x.GetAttribute("data-c")!, System.Globalization.CultureInfo.InvariantCulture))];

    private static string Css(IRenderedComponent<MusselSheetView> cut) => cut.Find("style").TextContent;
}
