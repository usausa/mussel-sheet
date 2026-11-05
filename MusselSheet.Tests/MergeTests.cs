namespace MusselSheet;

public sealed class MergeTests : BunitContext
{
    public MergeTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public void MergeKeepsOnlyTheTopLeftValue()
    {
        var sheet = new Sheet(3, 3);
        sheet.SetValues(0, 0, [["A1", "B1"], ["A2", "B2"]]);
        sheet.SetError(1, 1, "印");
        var kinds = new List<SheetChangeKind>();
        sheet.Changed += (_, e) => kinds.Add(e.Kind);

        sheet.Range("A1:B2").Merge();

        Assert.Equal("A1", sheet.GetValue(0, 0));
        Assert.Null(sheet.GetValue(0, 1));
        Assert.Null(sheet.GetValue(1, 0));
        Assert.Null(sheet.GetError(1, 1));
        Assert.Equal(CellRange.Parse("A1:B2"), sheet.GetMergeArea(1, 1));
        Assert.Null(sheet.GetMergeArea(2, 2));
        Assert.Equal([SheetChangeKind.Merges], kinds);
    }

    [Fact]
    public void MergeRejectsSingleCellsOutsideRangesAndPartialOverlaps()
    {
        var sheet = new Sheet(5, 5);
        sheet.Range("B2:C3").Merge();

        Assert.Throws<ArgumentException>(() => sheet.Range("A1").Merge());
        Assert.Throws<ArgumentOutOfRangeException>(() => sheet.Range("D4:F6").Merge());
        Assert.Throws<ArgumentOutOfRangeException>(() => sheet.Range("A:A").Merge());
        Assert.Throws<InvalidOperationException>(() => sheet.Range("C3:D4").Merge());

        sheet.Range("A1:D4").Merge();
        Assert.Equal([CellRange.Parse("A1:D4")], sheet.MergeAreas);

        sheet.Range("B2").Unmerge();
        Assert.Empty(sheet.MergeAreas);
    }

    [Fact]
    public void MergesFollowInsertedAndRemovedRowsAndColumns()
    {
        var sheet = new Sheet(6, 6);
        sheet.Range("B2:C4").Merge();

        sheet.InsertRows(2, 1);
        Assert.Equal(CellRange.Parse("B2:C5"), sheet.MergeAreas[0]);
        sheet.InsertColumns(0, 1);
        Assert.Equal(CellRange.Parse("C2:D5"), sheet.MergeAreas[0]);

        sheet.DeleteRows(1, 2);
        Assert.Equal(CellRange.Parse("C2:D3"), sheet.MergeAreas[0]);
        sheet.DeleteColumns(3, 1);
        Assert.Equal(CellRange.Parse("C2:C3"), sheet.MergeAreas[0]);
        sheet.DeleteRows(2, 1);
        Assert.Empty(sheet.MergeAreas);
    }

    [Fact]
    public void MergedCellIsRenderedAsOneCell()
    {
        var sheet = new Sheet(4, 3);
        sheet.Range("A1:B1").Merge();
        sheet.Range("B2:C4").Merge();
        sheet["B2"].Value = "備考";

        var cut = Render<MusselSheetView>(p => p.Add(x => x.Sheet, sheet));

        var anchor = cut.Find(".mussel-sheet-row[data-r='1'] .mussel-sheet-cell[data-c='1']");
        Assert.Contains("mussel-sheet-merge", anchor.ClassList);
        Assert.Equal("備考", anchor.TextContent);
        Assert.Equal("3", anchor.GetAttribute("aria-rowspan"));
        Assert.Equal("2", anchor.GetAttribute("aria-colspan"));
        var covered = cut.Find(".mussel-sheet-row[data-r='2'] .mussel-sheet-cell[data-c='2']");
        Assert.Contains("mussel-sheet-covered", covered.ClassList);
        Assert.Equal("true", covered.GetAttribute("aria-hidden"));
        Assert.Null(covered.GetAttribute("role"));
        Assert.Null(cut.Find(".mussel-sheet-row[data-r='0'] .mussel-sheet-cell[data-c='0']").GetAttribute("aria-rowspan"));

        var css = cut.Find("style").TextContent;
        Assert.Contains("--mussel-sheet-merge-w:200px;--mussel-sheet-merge-h:24px", css, StringComparison.Ordinal);
        Assert.Contains("--mussel-sheet-merge-w:200px;--mussel-sheet-merge-h:72px", css, StringComparison.Ordinal);
        Assert.Contains("mussel-sheet-tall", cut.Find(".mussel-sheet-row[data-r='1'] .mussel-sheet-rest").ClassList);
        Assert.DoesNotContain("mussel-sheet-tall", cut.Find(".mussel-sheet-row[data-r='0'] .mussel-sheet-rest").ClassList);
    }

    [Fact]
    public async Task OnlyTheTopLeftCellCanBeEdited()
    {
        var sheet = new Sheet(3, 3);
        sheet.Range("A1:B2").Merge();
        var cut = Render<MusselSheetView>(p => p.Add(x => x.Sheet, sheet));

        var result = await cut.InvokeAsync(() => cut.Instance.CommitAsync(1, 1, "x", false));
        Assert.False(result.Stored);
        await cut.InvokeAsync(() => cut.Instance.PasteTextAsync(CellRange.Parse("A1:C2"), "1\t2\t3\r\n4\t5\t6\r\n"));

        Assert.Equal("1", sheet.GetValue(0, 0));
        Assert.Null(sheet.GetValue(0, 1));
        Assert.Null(sheet.GetValue(1, 0));
        Assert.Equal("3", sheet.GetValue(0, 2));
        Assert.Equal("6", sheet.GetValue(1, 2));
    }

    [Fact]
    public async Task UndoingARowDeleteRestoresTheMerge()
    {
        var sheet = new Sheet(5, 3);
        sheet.Range("B2:C4").Merge();
        var cut = Render<MusselSheetView>(p => p.Add(x => x.Sheet, sheet).Add(x => x.AllowDeleteRows, true).Add(x => x.AllowDeleteColumns, true));

        await cut.InvokeAsync(() => cut.Instance.ChangeStructureAsync("deleteRows", 2, 0, 3, 2, 2, 0));
        Assert.Equal([CellRange.Parse("B2:C2")], sheet.MergeAreas);

        await cut.InvokeAsync(() => cut.Instance.UndoFromScriptAsync(false));
        Assert.Equal([CellRange.Parse("B2:C4")], sheet.MergeAreas);

        await cut.InvokeAsync(() => sheet.Range("B5:C5").Merge());
        await cut.InvokeAsync(() => cut.Instance.ChangeStructureAsync("deleteColumns", 0, 2, 4, 2, 0, 2));
        Assert.Equal([CellRange.Parse("B2:B4")], sheet.MergeAreas);

        await cut.InvokeAsync(() => cut.Instance.UndoFromScriptAsync(false));
        Assert.Equal([CellRange.Parse("B2:C4"), CellRange.Parse("B5:C5")], sheet.MergeAreas);
    }

    [Fact]
    public async Task VisibleMergeIsDrawnFromItsTopLeftCell()
    {
        var sheet = new Sheet(100, 30);
        sheet.Range(new CellRange(40, 10, 49, 15)).Merge();

        var cut = Render<MusselSheetView>(p => p
            .Add(x => x.Sheet, sheet)
            .Add(x => x.Width, "500px")
            .Add(x => x.Height, "264px")
            .Add(x => x.OverscanCount, 0)
            .Add(x => x.OverscanColumnCount, 0));

        await cut.InvokeAsync(() => cut.Instance.OnViewportChanged(24 * 46, 264, (100 * 13) + 50, 500));

        Assert.Contains("mussel-sheet-merge", cut.Find(".mussel-sheet-row[data-r='40'] .mussel-sheet-cell[data-c='10']").ClassList);
    }
}
