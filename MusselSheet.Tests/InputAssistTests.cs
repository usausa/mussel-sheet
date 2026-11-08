namespace MusselSheet;

using MusselSheet.Internal;

public sealed class InputAssistTests : BunitContext
{
    public InputAssistTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public async Task YearlessDateUsesTheBrowsersYear()
    {
        // The browser (UTC+9) is already in 2027 while the server clock (UTC) is still at 2026-12-31
        JSInterop.Setup<int>("timezoneOffset").SetResult(-540);
        var sheet = new Sheet(2, 1);
        sheet.Range("A1:A2").Configure(c => c.Kind = CellKind.Date);
        var cut = Render<MusselSheetView>(p => p.Add(x => x.Sheet, sheet));
        cut.Instance.Clock = new FixedClock(new DateTimeOffset(2026, 12, 31, 20, 0, 0, TimeSpan.Zero));

        await cut.InvokeAsync(() => cut.Instance.CommitAsync(0, 0, "1/5", false));
        await cut.InvokeAsync(() => cut.Instance.CommitAsync(1, 0, "２０２５０１０５", false));

        // The year-less text is not kept, so re-editing shows the full date and the day cannot shift with the year
        Assert.Equal(new DateOnly(2027, 1, 5), sheet.GetValue(0, 0));
        Assert.Null(sheet.GetInputText(0, 0));
        Assert.Equal("2027/01/05", sheet.GetEditText(0, 0));
        Assert.Equal(new DateOnly(2025, 1, 5), sheet.GetValue(1, 0));
        Assert.Equal("20250105", sheet.GetInputText(1, 0));
    }

    [Fact]
    public void MapCarriesTheInputMessageAndMoveGroup()
    {
        var sheet = new Sheet(2, 3);
        sheet.Range("A1:C1").Configure(c =>
        {
            c.MoveGroup = "見出し";
            c.InputMessage = "見出しの説明";
        });

        sheet.Range("C1").Configure(c =>
        {
            c.MoveGroup = string.Empty;
            c.InputMessage = string.Empty;
        });

        var map = SheetMapBuilder.Build(sheet, 24, 40, SheetValidationMode.Stay);
        var first = map.Patterns[map.RowPatterns[0]];
        Assert.Equal("見出し", map.Rules[first[0]].Group);
        Assert.Equal("見出しの説明", map.Rules[first[1]].Note);
        Assert.Null(map.Rules[first[2]].Group);
        Assert.Null(map.Rules[first[2]].Note);
        Assert.Null(map.Rules[map.Patterns[map.RowPatterns[1]][0]].Group);
        Assert.Null(sheet.GetSettings(0, 2).MoveGroup);
        Assert.Equal("見出しの説明", sheet.GetSettings(0, 0).InputMessage);
    }

    [Fact]
    public async Task UserChangesAreMarkedUntilAccepted()
    {
        var sheet = new Sheet(3, 2);
        sheet.SetValues(0, 0, [[1m, "a"]]);
        var cut = Render<MusselSheetView>(p => p.Add(x => x.Sheet, sheet).Add(x => x.ShowChanges, true));

        await cut.InvokeAsync(() => cut.Instance.CommitAsync(0, 0, "2", false));
        await cut.InvokeAsync(() => sheet.SetValue(1, 0, 5m));
        Assert.True(sheet["A1"].IsChanged);
        Assert.Equal(1m, sheet["A1"].OriginalValue);
        Assert.False(sheet.IsChanged(1, 0));
        Assert.Contains("mussel-sheet-changed", cut.Find(".mussel-sheet-row[data-r='0'] .mussel-sheet-cell[data-c='0']").ClassList);
        Assert.DoesNotContain("mussel-sheet-changed", cut.Find(".mussel-sheet-row[data-r='1'] .mussel-sheet-cell[data-c='0']").ClassList);

        await cut.InvokeAsync(() => cut.Instance.UndoFromScriptAsync(false));
        Assert.False(sheet.IsChanged(0, 0));
        Assert.DoesNotContain("mussel-sheet-changed", cut.Find(".mussel-sheet-row[data-r='0'] .mussel-sheet-cell[data-c='0']").ClassList);

        await cut.InvokeAsync(() => cut.Instance.ClearAsync(0, 1));
        await cut.InvokeAsync(() => cut.Instance.CommitAsync(2, 1, "b", false));
        Assert.Equal([new CellAddress(0, 1), new CellAddress(2, 1)], sheet.GetChangedCells());
        Assert.Null(sheet.GetValue(0, 1));
        Assert.Equal("a", sheet.GetOriginalValue(0, 1));

        await cut.InvokeAsync(sheet.AcceptChanges);
        Assert.Empty(sheet.GetChangedCells());
        Assert.DoesNotContain("mussel-sheet-changed", cut.Find(".mussel-sheet-row[data-r='2'] .mussel-sheet-cell[data-c='1']").ClassList);
    }

    [Fact]
    public async Task MarksMoveWithRowsAndCompareAppValuesWithTheOriginal()
    {
        var sheet = new Sheet(3, 1);
        sheet.SetValue(1, 0, "元");
        var cut = Render<MusselSheetView>(p => p.Add(x => x.Sheet, sheet));
        await cut.InvokeAsync(() => cut.Instance.CommitAsync(1, 0, "新", false));

        await cut.InvokeAsync(() => sheet.InsertRows(0, 1));
        Assert.True(sheet.IsChanged(2, 0));
        Assert.DoesNotContain("mussel-sheet-changed", cut.Find(".mussel-sheet-row[data-r='2'] .mussel-sheet-cell[data-c='0']").ClassList);

        await cut.InvokeAsync(() => sheet.SetValue(2, 0, "元"));
        Assert.False(sheet.IsChanged(2, 0));
        await cut.InvokeAsync(() => cut.Instance.CommitAsync(2, 0, "新", false));
        await cut.InvokeAsync(() => sheet.ClearContents(CellRange.Cell(2, 0)));
        Assert.True(sheet.IsChanged(2, 0));
        Assert.Equal("元", sheet.GetOriginalValue(2, 0));

        cut.Render(p => p.Add(x => x.ShowChanges, true));
        Assert.Contains("mussel-sheet-changed", cut.Find(".mussel-sheet-row[data-r='2'] .mussel-sheet-cell[data-c='0']").ClassList);
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
