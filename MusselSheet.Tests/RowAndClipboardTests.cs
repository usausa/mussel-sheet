namespace MusselSheet;

using MusselSheet.Internal;

public sealed class RowAndClipboardTests : BunitContext
{
    public RowAndClipboardTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public async Task EnterOnTheLastInputCellAddsARowBelowTheCurrentRow()
    {
        var sheet = DetailSheet();
        var cut = RenderSheet(sheet);

        Assert.True(await AddRow(cut, 1, 1));

        Assert.Equal(4, sheet.RowCount);
        Assert.Equal("合計", sheet.GetValue(3, 0));
        Assert.Equal(CellKind.Number, sheet.GetSettings(2, 1).Kind);
        Assert.False(sheet.GetSettings(2, 1).IsInputDisabled);
        Assert.Equal(30, sheet.Rows[2].Height);
        Assert.Null(sheet.GetValue(2, 1));
        Assert.True(cut.Instance.CanUndo);
    }

    [Fact]
    public async Task RowsAreAddedOnlyWhenEnabled()
    {
        var sheet = DetailSheet();
        var cut = RenderSheet(sheet, SheetLastRowEnterMode.Stop);

        Assert.False(await AddRow(cut, 1, 1));
        Assert.Equal(3, sheet.RowCount);
    }

    [Fact]
    public async Task RowsAreNotAddedFromCellsThatCannotBeInput()
    {
        var sheet = DetailSheet();
        sheet.Range("A2").Configure(c => c.ReadOnly = true);
        var cut = RenderSheet(sheet);

        Assert.False(await AddRow(cut, 1, 0));
        Assert.False(await AddRow(cut, 2, 1));
        Assert.Equal(3, sheet.RowCount);

        Assert.True(await AddRow(cut, 1, 1));
        Assert.Equal(4, sheet.RowCount);
    }

    [Fact]
    public async Task AppCanCancelTheRowAddition()
    {
        var sheet = DetailSheet();
        var seen = new List<SheetRowsInsertingEventArgs>();
        var cut = Render<MusselSheetView>(p => p
            .Add(x => x.Sheet, sheet)
            .Add(x => x.LastRowEnterMode, SheetLastRowEnterMode.AddRow)
            .Add(x => x.RowsInserting, e =>
            {
                seen.Add(e);
                e.Cancel = true;
            }));

        Assert.False(await AddRow(cut, 1, 1));

        var inserting = Assert.Single(seen);
        Assert.Equal(2, inserting.Index);
        Assert.Equal(1, inserting.Count);
        Assert.Equal(3, sheet.RowCount);
        Assert.False(cut.Instance.CanUndo);
    }

    [Fact]
    public async Task AddedRowCanBeUndoneAndRedoneWithItsValues()
    {
        var sheet = DetailSheet();
        var cut = RenderSheet(sheet);

        await Commit(cut, 1, 1, "5");
        await AddRow(cut, 1, 1);
        var added = sheet.Rows[2];
        await Commit(cut, 2, 1, "7");

        int[] newCell = [2, 1, 2, 1];
        int[] enterCell = [1, 1, 1, 1];
        Assert.Equal(newCell, await Undo(cut, false));
        Assert.Null(sheet.GetValue(2, 1));
        Assert.Equal(enterCell, await Undo(cut, false));
        Assert.Equal(3, sheet.RowCount);
        Assert.Equal(5m, sheet.GetValue(1, 1));

        Assert.Equal(newCell, await Undo(cut, true));
        Assert.Same(added, sheet.Rows[2]);
        Assert.Equal(CellKind.Number, sheet.GetSettings(2, 1).Kind);
        Assert.Equal(newCell, await Undo(cut, true));
        Assert.Equal(7m, sheet.GetValue(2, 1));
    }

    [Fact]
    public async Task RowRemovedByTheAppIsNotRestored()
    {
        var sheet = DetailSheet();
        var cut = RenderSheet(sheet);
        await AddRow(cut, 1, 1);

        await cut.InvokeAsync(() => sheet.DeleteRows(2, 1)).ConfigureAwait(true);

        Assert.Null(await Undo(cut, false));
        Assert.Null(await Undo(cut, true));
        Assert.Equal(3, sheet.RowCount);
    }

    [Fact]
    public async Task PasteGoesThroughTheCommitPipeline()
    {
        var sheet = DetailSheet();
        var committed = new List<CellCommittedEventArgs>();
        var cut = Render<MusselSheetView>(p => p.Add(x => x.Sheet, sheet).Add(x => x.CellCommitted, committed.Add));

        var result = await Paste(cut, 0, 1, "１，２００", true);

        Assert.True(result.Ok);
        Assert.Equal(1200m, sheet.GetValue(0, 1));
        Assert.Equal(CellChangeReason.Paste, Assert.Single(committed).Reason);
        Assert.True(cut.Instance.CanUndo);
    }

    [Fact]
    public async Task InvalidPasteFollowsTheValidationPolicy()
    {
        var sheet = DetailSheet();
        sheet.SetValue(0, 1, 3m);
        var cut = RenderSheet(sheet);

        var waiting = await Paste(cut, 0, 1, "abc", false);
        Assert.False(waiting.Stored);
        Assert.Equal(3m, sheet.GetValue(0, 1));

        var optimistic = await Paste(cut, 0, 1, "abc", true);
        Assert.True(optimistic.Stored);
        Assert.Equal("abc", sheet.GetValue(0, 1));
        Assert.Equal(JapaneseTexts.Default.Number, sheet.GetError(0, 1));
    }

    [Fact]
    public async Task PasteIntoAnInputDisabledCellIsRejected()
    {
        var sheet = DetailSheet();
        var cut = RenderSheet(sheet);

        var result = await Paste(cut, 2, 1, "1", true);

        Assert.False(result.Stored);
        Assert.Equal(JapaneseTexts.Default.NotEditable, result.Error);
    }

    [Fact]
    public async Task PasteIntoACheckCellConvertsTheText()
    {
        var sheet = new Sheet(1, 1);
        sheet["A1"].Configure(c => c.Kind = CellKind.Checkbox);
        var cut = RenderSheet(sheet);

        var result = await Paste(cut, 0, 0, "TRUE", true);

        Assert.True(result.Ok);
        Assert.Equal(true, sheet.GetValue(0, 0));
    }

    [Fact]
    public async Task CutClearsTheValueWithoutValidation()
    {
        var sheet = DetailSheet();
        sheet["B1"].Configure(c => c.Required = true);
        sheet.SetValue(0, 1, 3m);
        var committed = new List<CellCommittedEventArgs>();
        var cut = Render<MusselSheetView>(p => p.Add(x => x.Sheet, sheet).Add(x => x.CellCommitted, committed.Add));

        var result = await cut.InvokeAsync(() => cut.Instance.CutAsync(0, 1)).ConfigureAwait(true);

        Assert.True(result.Ok);
        Assert.Null(sheet.GetValue(0, 1));
        Assert.Equal(CellChangeReason.Cut, Assert.Single(committed).Reason);
        await Undo(cut, false);
        Assert.Equal(3m, sheet.GetValue(0, 1));
    }

    private static Sheet DetailSheet()
    {
        var sheet = new Sheet(3, 2);
        sheet.Range("B1:B2").Configure(c => c.Kind = CellKind.Number);
        sheet.Range("A3:B3").Configure(c => c.Kind = CellKind.Label);
        sheet.Rows[1].Height = 30;
        sheet.SetValue(2, 0, "合計");
        return sheet;
    }

    private static Task<bool> AddRow(IRenderedComponent<MusselSheetView> cut, int row, int column) =>
        cut.InvokeAsync(() => cut.Instance.AddRowFromScriptAsync(row, column));

    private static Task<CommitResult> Commit(IRenderedComponent<MusselSheetView> cut, int row, int column, string text) =>
        cut.InvokeAsync(() => cut.Instance.CommitAsync(row, column, text, true));

    private static Task<CommitResult> Paste(IRenderedComponent<MusselSheetView> cut, int row, int column, string text, bool optimistic) =>
        cut.InvokeAsync(() => cut.Instance.PasteAsync(row, column, text, optimistic));

    private static Task<int[]?> Undo(IRenderedComponent<MusselSheetView> cut, bool redo) =>
        cut.InvokeAsync(() => cut.Instance.UndoFromScriptAsync(redo));

    private IRenderedComponent<MusselSheetView> RenderSheet(Sheet sheet, SheetLastRowEnterMode lastRowEnter = SheetLastRowEnterMode.AddRow) =>
        Render<MusselSheetView>(p => p.Add(x => x.Sheet, sheet).Add(x => x.LastRowEnterMode, lastRowEnter));
}
