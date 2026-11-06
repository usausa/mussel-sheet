namespace MusselSheet;

using System.Text;

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

    private sealed class TextStream(string text) : IJSStreamReference
    {
        private readonly byte[] bytes = Encoding.UTF8.GetBytes(text);

        public bool Disposed { get; private set; }

        public long Length => bytes.Length;

        public ValueTask<Stream> OpenReadStreamAsync(long maxAllowedSize = 512000, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<Stream>(new MemoryStream(bytes));

        public ValueTask DisposeAsync()
        {
            Disposed = true;
            return ValueTask.CompletedTask;
        }
    }
}
