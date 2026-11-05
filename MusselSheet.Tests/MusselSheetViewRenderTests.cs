namespace MusselSheet;

using MusselSheet.Internal;

public sealed class MusselSheetViewRenderTests : BunitContext
{
    public MusselSheetViewRenderTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public void OtherAttributesGoToTheRootElement()
    {
        var sheet = new Sheet(1, 1);

        var cut = Render<MusselSheetView>(p => p
            .Add(x => x.Sheet, sheet)
            .AddUnmatched("id", "orders")
            .AddUnmatched("class", "my-sheet"));

        var root = cut.Find(".mussel-sheet");
        Assert.Equal("orders", root.Id);
        Assert.Contains("my-sheet", root.ClassList);
    }

    [Fact]
    public void RendersHeadersAndCells()
    {
        var sheet = new Sheet(3, 2);
        sheet.Columns[1].Header = "数量";
        sheet.SetValues(0, 0, [["りんご", 3m]]);

        var cut = Render<MusselSheetView>(p => p.Add(x => x.Sheet, sheet));

        string[] columnHeaders = ["A", "数量"];
        string[] rowHeaders = ["1", "2", "3"];
        Assert.Equal(columnHeaders, cut.FindAll(".mussel-sheet-colhead-cell").Select(x => x.TextContent));
        Assert.Equal(rowHeaders, cut.FindAll(".mussel-sheet-rowhead").Select(x => x.TextContent));
        Assert.Equal(6, cut.FindAll(".mussel-sheet-cell").Count);
        Assert.Equal("りんご", cut.Find(".mussel-sheet-row[data-r='0'] [data-c='0']").TextContent);
        Assert.Equal("3", cut.Find(".mussel-sheet-row[data-r='0'] [data-c='1']").TextContent);
    }

    [Fact]
    public void CellAttributesDescribeKindAndInput()
    {
        var sheet = new Sheet(1, 4);
        sheet["A1"].Configure(c =>
        {
            c.Kind = CellKind.Number;
            c.Format = "N0";
        });
        sheet["B1"].Configure(c => c.ReadOnly = true);
        sheet["C1"].Configure(c => c.ImeMode = ImeMode.Off);
        sheet["D1"].Configure(c => c.Kind = CellKind.Label);
        sheet.SetValue(0, 0, 1234567m);
        sheet.SetError(0, 2, "半角で入力してください");

        var cut = Render<MusselSheetView>(p => p.Add(x => x.Sheet, sheet));
        var cells = cut.FindAll(".mussel-sheet-cell");

        Assert.Equal("1,234,567", cells[0].TextContent);
        Assert.Equal("number", cells[0].GetAttribute("data-kind"));
        Assert.Equal("1234567", cells[0].GetAttribute("data-edit"));
        Assert.Contains("mussel-sheet-ha-r", cells[0].ClassList);
        Assert.Equal("1", cells[1].GetAttribute("data-ro"));
        Assert.Contains("mussel-sheet-ro", cells[1].ClassList);
        Assert.Null(cells[2].GetAttribute("data-kind"));
        Assert.Equal("half", cells[2].GetAttribute("data-ime"));
        Assert.Contains("mussel-sheet-error", cells[2].ClassList);
        Assert.Equal("半角で入力してください", cells[2].GetAttribute("title"));
        Assert.Equal("label", cells[3].GetAttribute("data-kind"));
        Assert.Equal("1", cells[3].GetAttribute("data-ro"));
    }

    [Fact]
    public void HeadersAndGridLinesCanBeHidden()
    {
        var sheet = new Sheet(2, 2);

        var cut = Render<MusselSheetView>(p => p
            .Add(x => x.Sheet, sheet)
            .Add(x => x.ShowRowHeaders, false)
            .Add(x => x.ShowColumnHeaders, false)
            .Add(x => x.ShowGridLines, false));

        Assert.Empty(cut.FindAll(".mussel-sheet-rowhead"));
        Assert.Empty(cut.FindAll(".mussel-sheet-colhead"));
        Assert.Contains("mussel-sheet-nogrid", cut.Find(".mussel-sheet").ClassList);
        Assert.Contains("grid-template-columns: 100px 100px}", cut.Find("style").TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public void ColorsAndBordersBecomeClasses()
    {
        var sheet = new Sheet(2, 2);
        sheet["A1"].Configure(c =>
        {
            c.Style.FillColor = "#fffbe6";
            c.Style.Borders.Right = CellBorder.Thick.WithColor("red");
        });

        var cut = Render<MusselSheetView>(p => p.Add(x => x.Sheet, sheet));
        var css = cut.Find("style").TextContent;
        var a1 = cut.Find(".mussel-sheet-row[data-r='0'] [data-c='0']");

        Assert.Contains("bg0", a1.ClassList);
        Assert.Contains("mussel-sheet-br-thick", a1.ClassList);
        Assert.Contains("brc1", a1.ClassList);
        Assert.Contains(".bg0{background-color:#fffbe6}", css, StringComparison.Ordinal);
        Assert.Contains(".brc1{border-right-color:red}", css, StringComparison.Ordinal);
        Assert.DoesNotContain("\n", css, StringComparison.Ordinal);
    }

    [Fact]
    public void UnsafeColorIsNotWritten()
    {
        var sheet = new Sheet(1, 1);
        sheet["A1"].Configure(c => c.Style.FillColor = "red}body{display:none");

        var cut = Render<MusselSheetView>(p => p.Add(x => x.Sheet, sheet));

        Assert.DoesNotContain("display", cut.Find("style").TextContent, StringComparison.Ordinal);
        Assert.DoesNotContain("bg0", cut.Find(".mussel-sheet-cell").ClassList);
    }

    [Fact]
    public async Task FixedHeightRendersOnlyVisibleRows()
    {
        var sheet = new Sheet(1000, 3);

        var cut = Render<MusselSheetView>(p => p
            .Add(x => x.Sheet, sheet)
            .Add(x => x.Height, "264px")
            .Add(x => x.OverscanCount, 2));

        // 240px (264px minus the 24px column header) holds 10 rows of 24px, plus 2 overscan rows below
        Assert.Equal(12, cut.FindAll(".mussel-sheet-row").Count);

        await cut.InvokeAsync(() => cut.Instance.OnViewportChanged(24 * 500, 264, 0, 1000)).ConfigureAwait(true);

        var rows = cut.FindAll(".mussel-sheet-row");
        Assert.Equal("498", rows[0].GetAttribute("data-r"));
        Assert.Equal("511", rows[^1].GetAttribute("data-r"));
        Assert.Equal("height:11952px", cut.FindAll(".mussel-sheet-spacer")[0].GetAttribute("style"));
    }

    [Fact]
    public void AutoHeightRendersAllRows()
    {
        var sheet = new Sheet(50, 2);

        var cut = Render<MusselSheetView>(p => p.Add(x => x.Sheet, sheet));

        Assert.Equal(50, cut.FindAll(".mussel-sheet-row").Count);
        Assert.Empty(cut.FindAll(".mussel-sheet-spacer"));
        Assert.Contains("mussel-sheet-autosize", cut.Find(".mussel-sheet").ClassList);
    }

    [Fact]
    public async Task SheetChangesAreRendered()
    {
        var sheet = new Sheet(2, 2);
        var cut = Render<MusselSheetView>(p => p.Add(x => x.Sheet, sheet));

        await cut.InvokeAsync(() =>
        {
            sheet.SetValue(1, 1, "変更");
            sheet.InsertRows(0, 1);
        }).ConfigureAwait(true);

        Assert.Equal(3, cut.FindAll(".mussel-sheet-row").Count);
        Assert.Equal("変更", cut.Find(".mussel-sheet-row[data-r='2'] [data-c='1']").TextContent);
        Assert.Equal("3", cut.FindAll(".mussel-sheet-rowhead")[2].TextContent);
    }

    [Fact]
    public async Task GridHasBasicAria()
    {
        var sheet = new Sheet(1000, 3);
        sheet["B2"].Configure(c => c.ReadOnly = true);
        sheet.SetError(1, 2, "エラー");

        var cut = Render<MusselSheetView>(p => p
            .Add(x => x.Sheet, sheet)
            .Add(x => x.Height, "264px")
            .Add(x => x.OverscanCount, 2));
        var grid = cut.Find("[role=grid]");

        // Counts include the header row and column even when rows are virtualized;
        // indexes are 1-based and count the headers too
        Assert.Equal("1001", grid.GetAttribute("aria-rowcount"));
        Assert.Equal("4", grid.GetAttribute("aria-colcount"));
        Assert.Equal("1", cut.Find(".mussel-sheet-colhead").GetAttribute("aria-rowindex"));
        string[] headerColumns = ["2", "3", "4"];
        Assert.Equal(headerColumns, cut.FindAll("[role=columnheader]").Select(x => x.GetAttribute("aria-colindex")));
        var row = cut.Find(".mussel-sheet-row[data-r='1']");
        Assert.Equal("3", row.GetAttribute("aria-rowindex"));
        Assert.Equal("1", row.QuerySelector("[role=rowheader]")?.GetAttribute("aria-colindex"));
        var cells = row.QuerySelectorAll("[role=gridcell]");
        Assert.Null(cells[0].GetAttribute("aria-readonly"));
        Assert.Equal("true", cells[1].GetAttribute("aria-readonly"));
        Assert.Equal("true", cells[2].GetAttribute("aria-invalid"));
        Assert.Equal("4", cells[2].GetAttribute("aria-colindex"));

        Assert.Empty(grid.QuerySelectorAll(".mussel-sheet-layer"));
        Assert.Equal("true", cut.Find(".mussel-sheet-corner").GetAttribute("aria-hidden"));

        await cut.InvokeAsync(() => cut.Instance.OnViewportChanged(24 * 500, 264, 0, 1000)).ConfigureAwait(true);

        Assert.Equal("500", cut.FindAll(".mussel-sheet-row")[0].GetAttribute("aria-rowindex"));
        Assert.All(cut.FindAll(".mussel-sheet-spacer"), x => Assert.Equal("true", x.GetAttribute("aria-hidden")));
    }

    [Fact]
    public void AriaIndexesSkipHiddenHeaders()
    {
        var sheet = new Sheet(2, 2);

        var cut = Render<MusselSheetView>(p => p
            .Add(x => x.Sheet, sheet)
            .Add(x => x.ShowRowHeaders, false)
            .Add(x => x.ShowColumnHeaders, false));
        var grid = cut.Find("[role=grid]");

        Assert.Equal("2", grid.GetAttribute("aria-rowcount"));
        Assert.Equal("2", grid.GetAttribute("aria-colcount"));
        Assert.Equal("1", cut.Find(".mussel-sheet-row[data-r='0']").GetAttribute("aria-rowindex"));
        string[] columns = ["1", "2"];
        Assert.Equal(columns, cut.FindAll(".mussel-sheet-row[data-r='0'] [role=gridcell]").Select(x => x.GetAttribute("aria-colindex")));
    }

    [Fact]
    public async Task OnlyTheChangedRowIsRenderedAgain()
    {
        var sheet = new Sheet(3, 2);
        var cut = Render<MusselSheetView>(p => p.Add(x => x.Sheet, sheet));
        var rows = cut.FindComponents<RowView>();

        await cut.InvokeAsync(() => sheet.SetValue(1, 0, "x")).ConfigureAwait(true);

        Assert.Equal(1, rows[0].RenderCount);
        Assert.Equal(2, rows[1].RenderCount);
        Assert.Equal(1, rows[2].RenderCount);
    }
}
