namespace MusselSheet;

using System.Text.Json;

using Microsoft.JSInterop;

using MusselSheet.Internal;

public sealed class RangeTests : BunitContext
{
    public RangeTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public async Task OneValueFillsTheSelection()
    {
        var sheet = NumberSheet(3, 2);
        var cut = Render<MusselSheetView>(p => p.Add(x => x.Sheet, sheet));

        var result = await Paste(cut, new CellRange(0, 0, 2, 1), "5\r\n");

        Assert.All(Values(sheet), x => Assert.Equal(5m, x));
        Assert.Equal(new PasteResult(0, 0, 2, 1, null), result);

        int[] range = [0, 0, 2, 1];
        Assert.Equal(range, await Undo(cut));
        Assert.All(Values(sheet), Assert.Null);
        Assert.False(cut.Instance.CanUndo);
    }

    [Fact]
    public async Task ARebindDuringARangeOperationRecordsNoStep()
    {
        var sheet = NumberSheet(2, 1);
        var cut = Render<MusselSheetView>(p => p.Add(x => x.Sheet, sheet).Add(x => x.CellCommitted, e =>
        {
            if (e.Address.Row == 0)
            {
                sheet.OnRebound();
            }
        }));

        await Paste(cut, new CellRange(0, 0, 1, 0), "1\r\n2\r\n");
        var pasted = cut.Instance.CanUndo;
        await cut.InvokeAsync(() => cut.Instance.ClearRangeAsync(0, 0, 1, 0));

        Assert.False(pasted);
        Assert.False(cut.Instance.CanUndo);
    }

    [Fact]
    public async Task AHandlerThatDeletesARowDuringAPasteLeavesAnUndoStep()
    {
        var sheet = NumberSheet(3, 1);
        var cut = Render<MusselSheetView>(p => p.Add(x => x.Sheet, sheet).Add(x => x.CellCommitting, e =>
        {
            if ((e.Address.Row == 1) && (sheet.RowCount == 3))
            {
                sheet.DeleteRows(2, 1);
            }
        }));

        await Paste(cut, new CellRange(1, 0, 2, 0), "1\r\n2\r\n");

        Assert.Equal(1m, sheet.GetValue(1, 0));
        Assert.True(cut.Instance.CanUndo);
    }

    [Fact]
    public async Task FillDownUsesTheSourceAsItWasWhenAHandlerInsertsRows()
    {
        var sheet = NumberSheet(4, 1);
        sheet.SetValue(0, 0, 7m);
        var inserted = false;
        var cut = Render<MusselSheetView>(p => p.Add(x => x.Sheet, sheet).Add(x => x.CellCommitted, _ =>
        {
            if (!inserted)
            {
                inserted = true;
                sheet.InsertRows(0, 1);
            }
        }));

        await cut.InvokeAsync(() => cut.Instance.FillDownAsync(0, 0, 3, 0));

        Assert.Equal(new object?[] { null, 7m, 7m, 7m, 7m }, Values(sheet));
    }

    [Fact]
    public async Task DeletingRowsTakesTheSelectedRowsEvenWhenAHandlerInsertsAbove()
    {
        var sheet = NumberSheet(3, 1);
        sheet.SetValue(0, 0, 1m);
        sheet.SetValue(1, 0, 2m);
        sheet.SetValue(2, 0, 3m);
        var cut = Render<MusselSheetView>(p => p.Add(x => x.Sheet, sheet).Add(x => x.AllowDeleteRows, true).Add(x => x.RowsDeleting, _ => sheet.InsertRows(0, 1)));

        await cut.InvokeAsync(() => cut.Instance.ChangeStructureAsync("deleteRows", 1, 0, 1, 0, 1, 0));

        Assert.Equal(new object?[] { null, 1m, 3m }, Values(sheet));
    }

    [Fact]
    public async Task TableStartsAtTheTopLeftAndIsTruncatedAtTheSheetEdge()
    {
        var sheet = NumberSheet(3, 2);
        var cut = Render<MusselSheetView>(p => p.Add(x => x.Sheet, sheet));

        var result = await Paste(cut, CellRange.Cell(1, 0), "1\t2\t3\r\n4\t5\t6\r\n7\t8\t9\r\n10\t11\t12\r\n");

        Assert.Equal(new PasteResult(1, 0, 2, 1, "シートからはみ出した分(2 行・1 列)は貼り付けませんでした"), result);
        Assert.Null(sheet.GetValue(0, 0));
        Assert.Equal(1m, sheet.GetValue(1, 0));
        Assert.Equal(2m, sheet.GetValue(1, 1));
        Assert.Equal(4m, sheet.GetValue(2, 0));
        Assert.Equal(5m, sheet.GetValue(2, 1));
    }

    [Fact]
    public async Task InputDisabledCellsAreSkippedAndErrorsAreMarked()
    {
        var sheet = NumberSheet(1, 3);
        sheet["B1"].Configure(c => c.ReadOnly = true);
        sheet["C1"].Configure(c => c.ValidationMode = SheetValidationMode.Revert);
        sheet.SetValues(0, 0, [[1m, 2m, 3m]]);
        var cut = Render<MusselSheetView>(p => p.Add(x => x.Sheet, sheet));

        var result = await Paste(cut, CellRange.Cell(0, 0), "x\ty\tz");

        Assert.Equal("x", sheet.GetValue(0, 0));
        Assert.Equal(JapaneseTexts.Default.Number, sheet.GetError(0, 0));
        Assert.Equal(2m, sheet.GetValue(0, 1));
        Assert.Equal(3m, sheet.GetValue(0, 2));
        Assert.Equal("1 セルは規則に合わないため、エラーの印を付けました。1 セルは規則に合わないため、貼り付けませんでした", result.Message);
    }

    [Fact]
    public async Task PasteRaisesEventsAndCanBeCanceled()
    {
        var sheet = NumberSheet(2, 2);
        var pasting = new List<SheetPastingEventArgs>();
        var committed = new List<CellCommittedEventArgs>();
        bool[] cancel = [false];
        var cut = Render<MusselSheetView>(p => p
            .Add(x => x.Sheet, sheet)
            .Add(x => x.CellCommitted, committed.Add)
            .Add(x => x.Pasting, x =>
            {
                pasting.Add(x);
                x.Cancel = cancel[0];
            }));

        await Paste(cut, CellRange.Cell(0, 0), "1\t2\r\n3\t4");

        var e = Assert.Single(pasting);
        Assert.Equal(new CellRange(0, 0, 1, 1), e.Range);
        string[][] values = [["1", "2"], ["3", "4"]];
        Assert.Equal(values, e.Values);
        Assert.Equal(4, committed.Count);
        Assert.All(committed, x => Assert.Equal(CellChangeReason.Paste, x.Reason));

        cancel[0] = true;
        var result = await Paste(cut, new CellRange(0, 0, 1, 1), "9");

        Assert.Equal(new PasteResult(0, 0, 1, 1, null), result);
        Assert.Equal(1m, sheet.GetValue(0, 0));
    }

    [Fact]
    public async Task TheMessageSaysWhatWasCut()
    {
        var sheet = NumberSheet(2, 2);
        var cut = Render<MusselSheetView>(p => p.Add(x => x.Sheet, sheet));

        var rows = await Paste(cut, new CellRange(1, 0, 1, 0), "1\r\n2\r\n3\r\n");
        var columns = await Paste(cut, new CellRange(0, 1, 0, 1), "1\t2\r\n");

        Assert.Equal("シートからはみ出した分(2 行)は貼り付けませんでした", rows.Message);
        Assert.Equal("シートからはみ出した分(1 列)は貼り付けませんでした", columns.Message);
    }

    [Fact]
    public async Task PasteRangeReadsTheStreamFromScript()
    {
        var sheet = NumberSheet(1, 2);
        var cut = Render<MusselSheetView>(p => p.Add(x => x.Sheet, sheet));
        var data = new TextStream("７\t８\r\n");
        try
        {
            var result = await cut.InvokeAsync(() => cut.Instance.PasteRangeAsync(0, 0, 0, 0, data)).ConfigureAwait(true);

            Assert.Equal(new PasteResult(0, 0, 0, 1, null), result);
            Assert.Equal(7m, sheet.GetValue(0, 0));
            Assert.Equal(8m, sheet.GetValue(0, 1));
            Assert.True(data.Disposed);
        }
        finally
        {
            await data.DisposeAsync().ConfigureAwait(true);
        }
    }

    [Fact]
    public async Task TextsOverTheSizeLimitAreRefusedWithAMessage()
    {
        const string message = "16 MiB を超える文字は入力できません";
        var sheet = NumberSheet(1, 2);
        var cut = Render<MusselSheetView>(p => p.Add(x => x.Sheet, sheet));
        var bridge = new SheetScriptBridge(cut.Instance);

        var range = await SendOversized(cut, x => bridge.PasteRangeAsync(0, 0, 0, 1, x, cut.Instance.SheetGeneration));
        var fill = await SendOversized(cut, x => bridge.FillAsync(0, 0, 0, 1, null, x, cut.Instance.SheetGeneration));
        var commit = await SendOversized(cut, x => bridge.CommitAsync(0, 0, null, false, x, cut.Instance.SheetGeneration));
        var paste = await SendOversized(cut, x => bridge.PasteAsync(0, 0, null, false, x, cut.Instance.SheetGeneration));
        var optimistic = await SendOversized(cut, x => bridge.CommitAsync(0, 1, null, true, x, cut.Instance.SheetGeneration));

        Assert.Equal(message, range.GetProperty("message").GetString());
        Assert.Equal(1, range.GetProperty("right").GetInt32());
        Assert.Equal(message, fill.GetProperty("message").GetString());
        Assert.All([commit, paste, optimistic], x => Assert.False(x.GetProperty("ok").GetBoolean()));
        Assert.All([commit, paste, optimistic], x => Assert.Equal(message, x.GetProperty("error").GetString()));
        Assert.Null(sheet.GetError(0, 0));
        Assert.Equal(message, sheet.GetError(0, 1));
        Assert.All(Values(sheet), Assert.Null);
        Assert.False(Renderer.UnhandledException.IsCompleted);
    }

    [Fact]
    public async Task ClearAndCutSkipInputDisabledCellsAndAreOneUndo()
    {
        var sheet = NumberSheet(2, 2);
        sheet["B2"].Configure(c => c.ReadOnly = true);
        sheet["A1"].Configure(c => c.Required = true);
        sheet.SetValues(0, 0, [[1m, 2m], [3m, 4m]]);
        var reasons = new List<CellChangeReason>();
        var cut = Render<MusselSheetView>(p => p.Add(x => x.Sheet, sheet).Add(x => x.CellCommitted, x => reasons.Add(x.Reason)));

        await cut.InvokeAsync(() => cut.Instance.ClearRangeAsync(0, 0, 1, 1)).ConfigureAwait(true);

        Assert.Null(sheet.GetValue(0, 0));
        Assert.Null(sheet.GetValue(0, 1));
        Assert.Null(sheet.GetValue(1, 0));
        Assert.Equal(4m, sheet.GetValue(1, 1));
        await Undo(cut);
        Assert.Equal(1m, sheet.GetValue(0, 0));
        Assert.Equal(3m, sheet.GetValue(1, 0));

        await cut.InvokeAsync(() => cut.Instance.CutRangeAsync(0, 0, 0, 1)).ConfigureAwait(true);

        Assert.Null(sheet.GetValue(0, 1));
        CellChangeReason[] expected = [.. Enumerable.Repeat(CellChangeReason.ClearContents, 3), .. Enumerable.Repeat(CellChangeReason.Undo, 3), .. Enumerable.Repeat(CellChangeReason.Cut, 2)];
        Assert.Equal(expected, reasons);
    }

    [Fact]
    public void RangeTextIsTsvAndHtmlOfTheEditText()
    {
        var sheet = new Sheet(2, 2);
        sheet["A1"].Configure(c =>
        {
            c.Kind = CellKind.Number;
            c.Format = "N0";
        });
        sheet.SetValues(0, 0, [[1234m, "0123"], [null, "a\tb"]]);
        var cut = Render<MusselSheetView>(p => p.Add(x => x.Sheet, sheet));

        var text = cut.Instance.GetRangeText(0, 0, 1, 1);

        Assert.Equal("1234\t0123\r\n\t\"a\tb\"\r\n", text.Text);
        Assert.Equal("<style>.istext{mso-number-format:\"\\@\";}br{mso-data-placement:same-cell;}</style><table><tr><td>1234</td><td class=\"istext\" style='mso-number-format:\"\\@\"'>0123</td></tr><tr><td class=\"istext\" style='mso-number-format:\"\\@\"'></td><td class=\"istext\" style='mso-number-format:\"\\@\"'>a\tb</td></tr></table>", text.Html);
    }

    [Fact]
    public async Task UndoSelectsTheWholeOperatedRange()
    {
        var sheet = NumberSheet(4, 2);
        sheet.SetValues(0, 0, [[1m, 2m], [3m, 4m]]);
        var cut = Render<MusselSheetView>(p => p.Add(x => x.Sheet, sheet));

        await cut.InvokeAsync(() => cut.Instance.ClearRangeAsync(0, 0, 2, 1));
        var cleared = await Undo(cut);
        int[] clearedRange = [0, 0, 2, 1];
        Assert.Equal(clearedRange, cleared);

        await cut.InvokeAsync(() => cut.Instance.FillDownAsync(0, 0, 3, 1));
        var filled = await Undo(cut);
        int[] filledRange = [0, 0, 3, 1];
        Assert.Equal(filledRange, filled);
    }

    [Fact]
    public async Task SelectionIsReportedWhenItChanges()
    {
        var sheet = NumberSheet(3, 2);
        var moved = new List<CellAddress>();
        var selected = new List<CellRange>();
        var cut = Render<MusselSheetView>(p => p
            .Add(x => x.Sheet, sheet)
            .Add(x => x.ActiveCellChanged, moved.Add)
            .Add(x => x.SelectionChanged, selected.Add));

        await cut.InvokeAsync(() => cut.Instance.OnActiveCellChangedAsync(0, 0, 0, 0, 2, 1)).ConfigureAwait(true);
        await cut.InvokeAsync(() => cut.Instance.OnActiveCellChangedAsync(0, 0, 0, 0, 2, 1)).ConfigureAwait(true);

        Assert.Equal(new CellRange(0, 0, 2, 1), cut.Instance.Selection);
        Assert.Equal(new CellRange(0, 0, 2, 1), Assert.Single(selected));
        Assert.Empty(moved);
    }

    private static Sheet NumberSheet(int rows, int columns)
    {
        var sheet = new Sheet(rows, columns);
        sheet.Configure(CellRange.All, c => c.Kind = CellKind.Number);
        return sheet;
    }

    private static IEnumerable<object?> Values(Sheet sheet) =>
        Enumerable.Range(0, sheet.RowCount).SelectMany(r => Enumerable.Range(0, sheet.ColumnCount).Select(c => sheet.GetValue(r, c)));

    private static Task<PasteResult> Paste(IRenderedComponent<MusselSheetView> cut, CellRange selection, string text) =>
        cut.InvokeAsync(() => cut.Instance.PasteTextAsync(selection, text));

    private static Task<int[]?> Undo(IRenderedComponent<MusselSheetView> cut) =>
        cut.InvokeAsync(() => cut.Instance.UndoFromScriptAsync(false));

    private static async Task<JsonElement> SendOversized(IRenderedComponent<MusselSheetView> cut, Func<IJSStreamReference, Task<JsonElement>> send)
    {
        var data = new OversizedStream();
        try
        {
            var result = await cut.InvokeAsync(() => send(data)).ConfigureAwait(true);
            Assert.True(data.Disposed);
            return result;
        }
        finally
        {
            await data.DisposeAsync().ConfigureAwait(true);
        }
    }

    private sealed class OversizedStream : IJSStreamReference
    {
        public bool Disposed { get; private set; }

        public long Length => (16 * 1024 * 1024) + 1;

        public ValueTask<Stream> OpenReadStreamAsync(long maxAllowedSize = 512000, CancellationToken cancellationToken = default) =>
            Length > maxAllowedSize ? throw new ArgumentOutOfRangeException(nameof(maxAllowedSize)) : ValueTask.FromResult<Stream>(new MemoryStream());

        public ValueTask DisposeAsync()
        {
            Disposed = true;
            return ValueTask.CompletedTask;
        }
    }
}
