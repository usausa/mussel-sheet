namespace MusselSheet;

using Bunit;

using MusselSheet.Internal;

public sealed class KeyOperationTests : BunitContext
{
    public KeyOperationTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public async Task CtrlEnterFillsTheSelectionWithTheText()
    {
        var sheet = NumberSheet(2, 3);
        sheet.Range("B1:B2").Configure(c => c.ReadOnly = true);
        var reasons = new List<CellChangeReason>();
        var cut = Render<MusselSheetView>(p => p.Add(x => x.Sheet, sheet).Add(x => x.CellCommitted, x => reasons.Add(x.Reason)));

        var result = await cut.InvokeAsync(() => cut.Instance.FillAsync(0, 0, 1, 2, "５")).ConfigureAwait(true);

        Assert.Equal(new PasteResult(0, 0, 1, 2, null), result);
        Assert.Equal(5m, sheet.GetValue(0, 0));
        Assert.Null(sheet.GetValue(0, 1));
        Assert.Equal(5m, sheet.GetValue(1, 2));
        Assert.Equal(4, reasons.Count);
        Assert.All(reasons, x => Assert.Equal(CellChangeReason.Input, x));
        await Undo(cut);
        Assert.Null(sheet.GetValue(1, 2));
        Assert.False(cut.Instance.CanUndo);
    }

    [Fact]
    public async Task CtrlEnterMarksCellsThatDoNotMatchTheirRules()
    {
        var sheet = new Sheet(1, 2);
        sheet["A1"].Configure(c => c.Kind = CellKind.Number);
        var cut = Render<MusselSheetView>(p => p.Add(x => x.Sheet, sheet));

        var result = await cut.InvokeAsync(() => cut.Instance.FillAsync(0, 0, 0, 1, "abc")).ConfigureAwait(true);

        Assert.Equal("abc", sheet.GetValue(0, 1));
        Assert.Equal(SheetTexts.Japanese.Number, sheet.GetError(0, 0));
        Assert.Equal("1 セルは規則に合わないため、エラーの印を付けました", result.Message);
    }

    [Fact]
    public async Task CtrlDCopiesTheRowAboveOrTheTopRowOfTheSelection()
    {
        var sheet = NumberSheet(4, 2);
        sheet.SetValues(0, 0, [[1m, 2m]]);
        var reasons = new List<CellChangeReason>();
        var cut = Render<MusselSheetView>(p => p.Add(x => x.Sheet, sheet).Add(x => x.CellCommitted, x => reasons.Add(x.Reason)));

        await cut.InvokeAsync(() => cut.Instance.FillDownAsync(1, 0, 1, 1)).ConfigureAwait(true);
        Assert.Equal(1m, sheet.GetValue(1, 0));
        Assert.Equal(2m, sheet.GetValue(1, 1));

        sheet.SetValues(1, 0, [[7m, 8m]]);
        await cut.InvokeAsync(() => cut.Instance.FillDownAsync(1, 0, 3, 1)).ConfigureAwait(true);
        Assert.Equal(7m, sheet.GetValue(3, 0));
        Assert.Equal(8m, sheet.GetValue(2, 1));
        Assert.Equal(1m, sheet.GetValue(0, 0));
        Assert.All(reasons, x => Assert.Equal(CellChangeReason.Fill, x));

        var top = await cut.InvokeAsync(() => cut.Instance.FillDownAsync(0, 0, 0, 1)).ConfigureAwait(true);
        Assert.Null(top.Message);
        Assert.Equal(1m, sheet.GetValue(0, 0));
    }

    [Fact]
    public async Task CtrlRCopiesTheColumnOnTheLeftThroughTheTargetRules()
    {
        var sheet = new Sheet(2, 3);
        sheet.Range("B1:C2").Configure(c => c.Kind = CellKind.Number);
        sheet.SetValues(0, 0, [["１２"], ["x"]]);
        var cut = Render<MusselSheetView>(p => p.Add(x => x.Sheet, sheet));

        var result = await cut.InvokeAsync(() => cut.Instance.FillRightAsync(0, 0, 1, 2)).ConfigureAwait(true);

        Assert.Equal(12m, sheet.GetValue(0, 1));
        Assert.Equal(12m, sheet.GetValue(0, 2));
        Assert.Equal("x", sheet.GetValue(1, 2));
        Assert.Equal(SheetTexts.Japanese.Number, sheet.GetError(1, 2));
        Assert.Equal("2 セルは規則に合わないため、エラーの印を付けました", result.Message);
    }

    [Theory]
    [InlineData(0, 0, "down", 2, 0)]
    [InlineData(2, 0, "down", 5, 0)]
    [InlineData(5, 0, "down", 7, 0)]
    [InlineData(7, 0, "down", 9, 0)]
    [InlineData(9, 0, "down", 9, 0)]
    [InlineData(9, 0, "up", 7, 0)]
    [InlineData(4, 0, "up", 2, 0)]
    [InlineData(0, 1, "right", 0, 3)]
    [InlineData(0, 3, "left", 0, 1)]
    [InlineData(0, 0, "end", 7, 3)]
    public void CtrlArrowFollowsTheEdgesOfTheData(int row, int column, string direction, int expectedRow, int expectedColumn)
    {
        var sheet = new Sheet(10, 4);
        foreach (var r in new[] { 0, 1, 2, 5, 6, 7 })
        {
            sheet.SetValue(r, 0, "x");
        }

        sheet.SetValues(0, 1, [["a", string.Empty, "c"]]);
        var cut = Render<MusselSheetView>(p => p.Add(x => x.Sheet, sheet));

        int[] expected = [expectedRow, expectedColumn];
        Assert.Equal(expected, cut.Instance.FindEdge(row, column, direction));
    }

    [Fact]
    public async Task MultilineCellsKeepLineBreaksAndOthersDropThem()
    {
        var sheet = new Sheet(1, 2);
        sheet["A1"].Configure(c => c.WrapText = true);
        var cut = Render<MusselSheetView>(p => p.Add(x => x.Sheet, sheet));

        await cut.InvokeAsync(() => cut.Instance.CommitAsync(0, 0, "1行目\r\n2行目", true)).ConfigureAwait(true);
        await cut.InvokeAsync(() => cut.Instance.CommitAsync(0, 1, "1行目\r\n2行目", true)).ConfigureAwait(true);

        Assert.Equal("1行目\n2行目", sheet.GetValue(0, 0));
        Assert.Equal("1行目2行目", sheet.GetValue(0, 1));
        Assert.Contains("mussel-sheet-ml", cut.Find(".mussel-sheet-row[data-r='0'] [data-c='0']").ClassList);
        Assert.DoesNotContain("mussel-sheet-ml", cut.Find(".mussel-sheet-row[data-r='0'] [data-c='1']").ClassList);
    }

    [Fact]
    public async Task PastedTableKeepsLineBreaksOnlyInMultilineCells()
    {
        var sheet = new Sheet(1, 2);
        sheet["A1"].Configure(c => c.WrapText = true);
        var cut = Render<MusselSheetView>(p => p.Add(x => x.Sheet, sheet));

        await cut.InvokeAsync(() => cut.Instance.PasteTextAsync(CellRange.Cell(0, 0), "\"a\nb\"\t\"c\nd\"")).ConfigureAwait(true);

        Assert.Equal("a\nb", sheet.GetValue(0, 0));
        Assert.Equal("cd", sheet.GetValue(0, 1));
    }

    private static Sheet NumberSheet(int rows, int columns)
    {
        var sheet = new Sheet(rows, columns);
        sheet.Configure(CellRange.All, c => c.Kind = CellKind.Number);
        return sheet;
    }

    private static Task<int[]?> Undo(IRenderedComponent<MusselSheetView> cut) =>
        cut.InvokeAsync(() => cut.Instance.UndoFromScriptAsync(false));
}
