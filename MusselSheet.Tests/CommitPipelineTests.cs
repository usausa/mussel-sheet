namespace MusselSheet;

using MusselSheet.Internal;

public sealed class CommitPipelineTests : BunitContext
{
    public CommitPipelineTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public async Task InputIsNormalizedConvertedAndStored()
    {
        var sheet = NumberSheet();
        var committed = new List<CellCommittedEventArgs>();
        var cut = Render<MusselSheetView>(p => p.Add(x => x.Sheet, sheet).Add(x => x.CellCommitted, committed.Add));

        var result = await Commit(cut, 0, 0, "１，２３４", true);

        Assert.True(result.Ok);
        Assert.Equal(1234m, sheet.GetValue(0, 0));
        Assert.Equal("1,234", sheet.GetInputText(0, 0));
        var change = Assert.Single(committed);
        Assert.Null(change.OldValue);
        Assert.Equal(1234m, change.Value);
        Assert.Equal(CellChangeReason.Input, change.Reason);
    }

    [Fact]
    public async Task WaitingCommitKeepsTheOldValueOnError()
    {
        var sheet = NumberSheet();
        sheet.SetValue(0, 0, 5m);
        var cut = Render<MusselSheetView>(p => p.Add(x => x.Sheet, sheet));

        var result = await Commit(cut, 0, 0, "abc", false);

        Assert.False(result.Ok);
        Assert.False(result.Stored);
        Assert.Equal(JapaneseTexts.Default.Number, result.Error);
        Assert.Equal(5m, sheet.GetValue(0, 0));
        Assert.Null(sheet.GetError(0, 0));
    }

    [Fact]
    public async Task OptimisticCommitKeepsTheTextWithAnErrorMark()
    {
        var sheet = NumberSheet();
        var cut = Render<MusselSheetView>(p => p.Add(x => x.Sheet, sheet));

        var result = await Commit(cut, 0, 0, "abc", true);

        Assert.False(result.Ok);
        Assert.True(result.Stored);
        Assert.Equal("abc", sheet.GetValue(0, 0));
        Assert.Equal(JapaneseTexts.Default.Number, sheet.GetError(0, 0));
    }

    [Fact]
    public async Task RevertPolicyNeverStoresInvalidInput()
    {
        var sheet = NumberSheet();
        sheet.SetValue(0, 0, 5m);
        sheet["A1"].Configure(c => c.ValidationMode = SheetValidationMode.Revert);
        var cut = Render<MusselSheetView>(p => p.Add(x => x.Sheet, sheet));

        var result = await Commit(cut, 0, 0, "abc", true);

        Assert.False(result.Stored);
        Assert.Equal(5m, sheet.GetValue(0, 0));
        Assert.Null(sheet.GetError(0, 0));
    }

    [Fact]
    public async Task AcceptAndMarkStoresEvenWhenWaiting()
    {
        var sheet = NumberSheet();
        var cut = Render<MusselSheetView>(p => p.Add(x => x.Sheet, sheet).Add(x => x.ValidationMode, SheetValidationMode.AcceptAndMark));

        var result = await Commit(cut, 0, 0, "abc", false);

        Assert.True(result.Stored);
        Assert.Equal("abc", sheet.GetValue(0, 0));
        Assert.Equal(JapaneseTexts.Default.Number, sheet.GetError(0, 0));
    }

    [Fact]
    public async Task CorrectInputClearsTheErrorMark()
    {
        var sheet = NumberSheet();
        var cut = Render<MusselSheetView>(p => p.Add(x => x.Sheet, sheet));
        await Commit(cut, 0, 0, "abc", true);

        await Commit(cut, 0, 0, "12", true);

        Assert.Equal(12m, sheet.GetValue(0, 0));
        Assert.Null(sheet.GetError(0, 0));
    }

    [Fact]
    public async Task CommittingEventCanReplaceTheValueOrCancel()
    {
        var sheet = NumberSheet();
        var replacements = new Dictionary<string, decimal> { ["1"] = 100m };
        var cut = Render<MusselSheetView>(p => p.Add(x => x.Sheet, sheet).Add(x => x.CellCommitting, x => OnCommitting(x, replacements)));

        await Commit(cut, 0, 0, "1", true);
        Assert.Equal(100m, sheet.GetValue(0, 0));

        var result = await Commit(cut, 0, 0, "7", true);
        Assert.False(result.Stored);
        Assert.Equal("7 は使えません", result.Error);
        Assert.Equal(100m, sheet.GetValue(0, 0));
        Assert.Equal("7 は使えません", sheet.GetError(0, 0));

        static void OnCommitting(CellCommittingEventArgs e, Dictionary<string, decimal> replacements)
        {
            if (replacements.TryGetValue(e.Text, out var value))
            {
                e.Value = value;
            }
            else if (e.Text == "7")
            {
                e.Cancel = true;
                e.Error = "7 は使えません";
            }
        }
    }

    [Fact]
    public async Task ValidatorRunsAfterTheBuiltInRules()
    {
        var sheet = new Sheet(1, 1);
        sheet["A1"].Configure(c => c.Validator = x => x.Text.StartsWith('A') ? null : "A で始めてください");
        var cut = Render<MusselSheetView>(p => p.Add(x => x.Sheet, sheet));

        var rejected = await Commit(cut, 0, 0, "B1", false);
        var accepted = await Commit(cut, 0, 0, "A1", false);

        Assert.Equal("A で始めてください", rejected.Error);
        Assert.True(accepted.Ok);
        Assert.Equal("A1", sheet.GetValue(0, 0));
    }

    [Fact]
    public async Task ClearSkipsValidation()
    {
        var sheet = NumberSheet();
        sheet["A1"].Configure(c => c.Required = true);
        sheet.SetValue(0, 0, 5m);
        var committed = new List<CellCommittedEventArgs>();
        var cut = Render<MusselSheetView>(p => p.Add(x => x.Sheet, sheet).Add(x => x.CellCommitted, committed.Add));

        var result = await cut.InvokeAsync(() => cut.Instance.ClearAsync(0, 0)).ConfigureAwait(true);

        Assert.True(result.Ok);
        Assert.Null(sheet.GetValue(0, 0));
        Assert.Equal(CellChangeReason.ClearContents, Assert.Single(committed).Reason);
    }

    [Fact]
    public async Task ToggleFlipsTheCheck()
    {
        var sheet = new Sheet(1, 1);
        sheet["A1"].Configure(c => c.Kind = CellKind.Checkbox);
        var cut = Render<MusselSheetView>(p => p.Add(x => x.Sheet, sheet));

        await cut.InvokeAsync(() => cut.Instance.ToggleAsync(0, 0)).ConfigureAwait(true);
        Assert.Equal(true, sheet.GetValue(0, 0));

        await cut.InvokeAsync(() => cut.Instance.ToggleAsync(0, 0)).ConfigureAwait(true);
        Assert.Equal(false, sheet.GetValue(0, 0));
    }

    [Fact]
    public async Task InputDisabledCellIsRejected()
    {
        var sheet = new Sheet(1, 2);
        sheet["A1"].Configure(c => c.ReadOnly = true);
        sheet["B1"].Configure(c => c.Kind = CellKind.Label);
        var cut = Render<MusselSheetView>(p => p.Add(x => x.Sheet, sheet));

        var readOnly = await Commit(cut, 0, 0, "x", true);
        var label = await Commit(cut, 0, 1, "x", true);

        Assert.Equal(JapaneseTexts.Default.NotEditable, readOnly.Error);
        Assert.False(label.Stored);
        Assert.Null(sheet.GetValue(0, 0));
        Assert.Null(sheet.GetValue(0, 1));
    }

    [Fact]
    public async Task UndoAndRedoRestoreValues()
    {
        var sheet = NumberSheet();
        var reasons = new List<CellChangeReason>();
        var cut = Render<MusselSheetView>(p => p.Add(x => x.Sheet, sheet).Add(x => x.CellCommitted, x => reasons.Add(x.Reason)));
        await Commit(cut, 0, 0, "1", true);
        await Commit(cut, 0, 0, "2", true);

        var target = await cut.InvokeAsync(() => cut.Instance.UndoFromScriptAsync(false)).ConfigureAwait(true);
        Assert.Equal(1m, sheet.GetValue(0, 0));
        int[] origin = [0, 0, 0, 0];
        Assert.Equal(origin, target);

        await cut.InvokeAsync(() => cut.Instance.UndoFromScriptAsync(false)).ConfigureAwait(true);
        Assert.Null(sheet.GetValue(0, 0));
        Assert.False(cut.Instance.CanUndo);

        await cut.InvokeAsync(() => cut.Instance.UndoFromScriptAsync(true)).ConfigureAwait(true);
        Assert.Equal(1m, sheet.GetValue(0, 0));
        Assert.True(cut.Instance.CanRedo);

        CellChangeReason[] expected = [CellChangeReason.Input, CellChangeReason.Input, CellChangeReason.Undo, CellChangeReason.Undo, CellChangeReason.Redo];
        Assert.Equal(expected, reasons);
    }

    [Fact]
    public async Task UndoFollowsTheCellAfterRowsAreInserted()
    {
        var sheet = NumberSheet();
        var cut = Render<MusselSheetView>(p => p.Add(x => x.Sheet, sheet));
        await Commit(cut, 1, 0, "1", true);
        await cut.InvokeAsync(() => sheet.InsertRows(0, 1)).ConfigureAwait(true);

        var target = await cut.InvokeAsync(() => cut.Instance.UndoFromScriptAsync(false)).ConfigureAwait(true);

        int[] moved = [2, 0, 2, 0];
        Assert.Equal(moved, target);
        Assert.Null(sheet.GetValue(2, 0));
    }

    [Fact]
    public async Task SameValueIsNotRecordedTwice()
    {
        var sheet = NumberSheet();
        var committed = new List<CellCommittedEventArgs>();
        var cut = Render<MusselSheetView>(p => p.Add(x => x.Sheet, sheet).Add(x => x.CellCommitted, committed.Add));

        await Commit(cut, 0, 0, "1", true);
        await Commit(cut, 0, 0, "1", true);
        await cut.InvokeAsync(() => cut.Instance.UndoFromScriptAsync(false)).ConfigureAwait(true);

        Assert.Single(committed, x => x.Reason == CellChangeReason.Input);
        Assert.False(cut.Instance.CanUndo);
    }

    [Fact]
    public async Task ConfirmingAnAppValueWithTheSameValueDoesNothing()
    {
        var sheet = new Sheet(1, 1);
        sheet["A1"].Configure(c => c.Kind = CellKind.Date);
        sheet.SetValue(0, 0, new DateOnly(2026, 11, 2));
        var committed = new List<CellCommittedEventArgs>();
        var cut = Render<MusselSheetView>(p => p.Add(x => x.Sheet, sheet).Add(x => x.CellCommitted, committed.Add));

        var result = await Commit(cut, 0, 0, "2026/11/02", true);

        Assert.True(result.Ok);
        Assert.Empty(committed);
        Assert.False(cut.Instance.CanUndo);
        Assert.Null(sheet.GetInputText(0, 0));
    }

    [Fact]
    public async Task ActiveCellChangeIsReported()
    {
        var sheet = NumberSheet();
        var reported = new List<CellAddress>();
        var cut = Render<MusselSheetView>(p => p.Add(x => x.Sheet, sheet).Add(x => x.ActiveCellChanged, reported.Add));

        await cut.InvokeAsync(() => cut.Instance.OnActiveCellChangedAsync(2, 0, 2, 0, 2, 0)).ConfigureAwait(true);

        Assert.Equal(new CellAddress(2, 0), cut.Instance.ActiveCell);
        Assert.Equal(new CellAddress(2, 0), Assert.Single(reported));
        Assert.Equal(CellRange.Cell(2, 0), cut.Instance.Selection);
    }

    [Fact]
    public void EditTextIsAvailableForCellsThatAreNotRendered()
    {
        var sheet = NumberSheet();
        sheet["A1"].Configure(c => c.Format = "N0");
        sheet.SetValue(0, 0, 1234m);
        var cut = Render<MusselSheetView>(p => p.Add(x => x.Sheet, sheet));

        Assert.Equal("1234", cut.Instance.GetEditText(0, 0));
        Assert.Equal(string.Empty, cut.Instance.GetEditText(10, 0));
    }

    private static Sheet NumberSheet()
    {
        var sheet = new Sheet(3, 1);
        sheet.Configure(CellRange.All, c => c.Kind = CellKind.Number);
        return sheet;
    }

    private static Task<CommitResult> Commit(IRenderedComponent<MusselSheetView> cut, int row, int column, string text, bool optimistic) =>
        cut.InvokeAsync(() => cut.Instance.CommitAsync(row, column, text, optimistic));
}
