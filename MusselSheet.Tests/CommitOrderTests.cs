namespace MusselSheet;

using MusselSheet.Internal;

public sealed class CommitOrderTests : BunitContext
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    public CommitOrderTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public async Task AnUndoWaitsForTheCommitBeforeIt()
    {
        var sheet = new Sheet(2, 1);
        var held = new TaskCompletionSource();
        var cut = Render<MusselSheetView>(p => p.Add(x => x.Sheet, sheet).Add(x => x.CellCommitting, _ => held.Task));
        var bridge = new SheetScriptBridge(cut.Instance);
        Task? commit = null;
        Task<int[]?>? undo = null;

        await cut.InvokeAsync(() =>
        {
            commit = bridge.CommitAsync(0, 0, "5", true, null);
            undo = bridge.UndoFromScriptAsync(false);
        });
        Assert.False(undo!.IsCompleted);

        await cut.InvokeAsync(held.SetResult);
        await commit!.WaitAsync(Timeout, Xunit.TestContext.Current.CancellationToken);
        var target = await undo.WaitAsync(Timeout, Xunit.TestContext.Current.CancellationToken);

        Assert.NotNull(target);
        Assert.Equal([0, 0, 0, 0], target);
        Assert.Null(sheet.GetValue(0, 0));
    }

    [Fact]
    public async Task CancelingTheCustomEditorWaitsForItsCommit()
    {
        var sheet = new Sheet(2, 1);
        sheet["A2"].Configure(c => c.Kind = CellKind.Custom);
        var held = new TaskCompletionSource();
        CellEditContext? edit = null;
        var cut = Render<MusselSheetView>(p => p
            .Add(x => x.Sheet, sheet)
            .Add(x => x.CustomEditor, context =>
            {
                edit = context;
                return static _ => { };
            })
            .Add(x => x.CellCommitting, e => e.Address.Row == 0 ? held.Task : Task.CompletedTask));
        var bridge = new SheetScriptBridge(cut.Instance);
        await cut.InvokeAsync(() => bridge.OpenCustomEditorAsync(1, 0));
        edit!.Value = "red";
        Task[] calls = [];

        await cut.InvokeAsync(() =>
        {
            calls = [bridge.CommitAsync(0, 0, "5", true, null), bridge.CommitCustomEditorAsync(false), bridge.CancelCustomEditorAsync()];
        });
        await cut.InvokeAsync(held.SetResult);
        await Task.WhenAll(calls).WaitAsync(Timeout, Xunit.TestContext.Current.CancellationToken);

        Assert.Equal("red", sheet.GetValue(1, 0));
    }

    [Fact]
    public async Task CancelingFromTheTemplateWaitsForTheCommitBeforeIt()
    {
        var sheet = new Sheet(2, 1);
        sheet["A2"].Configure(c => c.Kind = CellKind.Custom);
        var held = new TaskCompletionSource();
        CellEditContext? edit = null;
        var cut = Render<MusselSheetView>(p => p
            .Add(x => x.Sheet, sheet)
            .Add(x => x.CustomEditor, context =>
            {
                edit = context;
                return static _ => { };
            })
            .Add(x => x.CellCommitting, e => e.Address.Row == 0 ? held.Task : Task.CompletedTask));
        var bridge = new SheetScriptBridge(cut.Instance);
        await cut.InvokeAsync(() => bridge.OpenCustomEditorAsync(1, 0));
        edit!.Value = "red";
        Task[] calls = [];

        await cut.InvokeAsync(() =>
        {
            calls = [bridge.CommitAsync(0, 0, "5", true, null), bridge.CommitCustomEditorAsync(false), edit.CancelAsync()];
        });
        await cut.InvokeAsync(held.SetResult);
        await Task.WhenAll(calls).WaitAsync(Timeout, Xunit.TestContext.Current.CancellationToken);

        Assert.Equal("red", sheet.GetValue(1, 0));
    }

    [Fact]
    public async Task AnInsertInTheCommittingHandlerKeepsTheValueInItsCell()
    {
        var sheet = new Sheet(3, 1);
        var inserted = false;
        var cut = Render<MusselSheetView>(p => p.Add(x => x.Sheet, sheet).Add(x => x.CellCommitting, _ => InsertOnce(sheet, ref inserted)));
        var bridge = new SheetScriptBridge(cut.Instance);

        await cut.InvokeAsync(() => bridge.CommitAsync(1, 0, "x", true, null));

        Assert.Null(sheet.GetValue(1, 0));
        Assert.Equal("x", sheet.GetValue(2, 0));
    }

    [Fact]
    public async Task AnInsertWhileARangeIsPastedKeepsTheValuesInTheirCells()
    {
        var sheet = new Sheet(3, 1);
        var inserted = false;
        var cut = Render<MusselSheetView>(p => p.Add(x => x.Sheet, sheet).Add(x => x.CellCommitting, _ => InsertOnce(sheet, ref inserted)));

        await cut.InvokeAsync(() => cut.Instance.PasteTextAsync(new CellRange(1, 0, 2, 0), "a\r\nb\r\n"));

        Assert.Null(sheet.GetValue(1, 0));
        Assert.Equal("a", sheet.GetValue(2, 0));
        Assert.Equal("b", sheet.GetValue(3, 0));
    }

    [Fact]
    public async Task TwoViewsOfOneSheetShareTheGate()
    {
        var sheet = new Sheet(2, 1);
        var held = new TaskCompletionSource();
        var first = Render<MusselSheetView>(p => p.Add(x => x.Sheet, sheet).Add(x => x.CellCommitting, _ => held.Task));
        var second = Render<MusselSheetView>(p => p.Add(x => x.Sheet, sheet));
        var a = new SheetScriptBridge(first.Instance);
        var b = new SheetScriptBridge(second.Instance);
        Task? commit = null;
        Task<string>? text = null;

        await first.InvokeAsync(() =>
        {
            commit = a.CommitAsync(0, 0, "5", true, null);
            text = b.GetEditTextAsync(0, 0);
        });
        Assert.False(text!.IsCompleted);

        await first.InvokeAsync(held.SetResult);
        await commit!.WaitAsync(Timeout, Xunit.TestContext.Current.CancellationToken);

        Assert.Equal("5", await text.WaitAsync(Timeout, Xunit.TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ACommitFromTheScriptThatWaitsWhileItsViewIsDisposedWritesNothing()
    {
        var sheet = new Sheet(2, 1);
        var held = new TaskCompletionSource();
        var first = Render<MusselSheetView>(p => p.Add(x => x.Sheet, sheet).Add(x => x.CellCommitting, _ => held.Task));
        var second = Render<MusselSheetView>(p => p.Add(x => x.Sheet, sheet));
        var a = new SheetScriptBridge(first.Instance);
        var b = new SheetScriptBridge(second.Instance);
        Task? waiting = null;
        Task? queued = null;

        await first.InvokeAsync(() =>
        {
            waiting = a.CommitAsync(0, 0, "5", true, null);
            queued = b.CommitAsync(1, 0, "7", true, null);
        });
        await second.InvokeAsync(() => second.Instance.DisposeAsync().AsTask());
        await first.InvokeAsync(held.SetResult);
        await waiting!.WaitAsync(Timeout, Xunit.TestContext.Current.CancellationToken);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => queued!);
        Assert.Null(sheet.GetValue(1, 0));
    }

    [Fact]
    public async Task AnUndoThatWaitsWhileItsViewIsDisposedDoesNothing()
    {
        var sheet = new Sheet(2, 1);
        var held = new TaskCompletionSource();
        var first = Render<MusselSheetView>(p => p.Add(x => x.Sheet, sheet).Add(x => x.CellCommitting, _ => held.Task));
        var second = Render<MusselSheetView>(p => p.Add(x => x.Sheet, sheet));
        var a = new SheetScriptBridge(first.Instance);
        var b = new SheetScriptBridge(second.Instance);
        await second.InvokeAsync(() => b.CommitAsync(0, 0, "5", false, null));
        var stored = sheet.GetValue(0, 0);
        Task? waiting = null;
        Task<bool>? queued = null;

        await first.InvokeAsync(() =>
        {
            waiting = a.CommitAsync(1, 0, "7", true, null);
            queued = second.Instance.UndoAsync();
        });
        await second.InvokeAsync(() => second.Instance.DisposeAsync().AsTask());
        await first.InvokeAsync(held.SetResult);
        await waiting!.WaitAsync(Timeout, Xunit.TestContext.Current.CancellationToken);

        Assert.False(await queued!.WaitAsync(Timeout, Xunit.TestContext.Current.CancellationToken));
        Assert.NotNull(stored);
        Assert.Equal(stored, sheet.GetValue(0, 0));
    }

    [Fact]
    public async Task ACommitFromTheTemplateThatWaitsWhileItsViewIsDisposedWritesNothing()
    {
        var sheet = new Sheet(2, 1);
        sheet["A2"].Configure(c => c.Kind = CellKind.Custom);
        var held = new TaskCompletionSource();
        CellEditContext? edit = null;
        var first = Render<MusselSheetView>(p => p.Add(x => x.Sheet, sheet).Add(x => x.CellCommitting, _ => held.Task));
        var second = Render<MusselSheetView>(p => p
            .Add(x => x.Sheet, sheet)
            .Add(x => x.CustomEditor, context =>
            {
                edit = context;
                return static _ => { };
            }));
        var a = new SheetScriptBridge(first.Instance);
        var b = new SheetScriptBridge(second.Instance);
        await second.InvokeAsync(() => b.OpenCustomEditorAsync(1, 0));
        edit!.Value = "red";
        Task? waiting = null;
        Task<bool>? queued = null;

        await first.InvokeAsync(() =>
        {
            waiting = a.CommitAsync(0, 0, "5", true, null);
            queued = edit.CommitAsync();
        });
        await second.InvokeAsync(() => second.Instance.DisposeAsync().AsTask());
        await first.InvokeAsync(held.SetResult);
        await waiting!.WaitAsync(Timeout, Xunit.TestContext.Current.CancellationToken);

        Assert.False(await queued!.WaitAsync(Timeout, Xunit.TestContext.Current.CancellationToken));
        Assert.Null(sheet.GetValue(1, 0));
    }

    [Fact]
    public async Task TheGateOpensAgainAfterAHandlerThrows()
    {
        var sheet = new Sheet(2, 1);
        var cut = Render<MusselSheetView>(p => p.Add(x => x.Sheet, sheet).Add(x => x.CellCommitting, e => e.Address.Row == 0 ? throw new InvalidOperationException("app") : Task.CompletedTask));
        var bridge = new SheetScriptBridge(cut.Instance);

        await Assert.ThrowsAsync<InvalidOperationException>(() => cut.InvokeAsync(() => bridge.CommitAsync(0, 0, "1", true, null)));
        await cut.InvokeAsync(() => bridge.CommitAsync(1, 0, "2", true, null)).WaitAsync(Timeout, Xunit.TestContext.Current.CancellationToken);

        Assert.Equal("2", sheet.GetValue(1, 0));
    }

    [Fact]
    public async Task CallsFromTheScriptAfterTheViewIsGoneDoNothing()
    {
        var sheet = new Sheet(1, 1);
        var committed = 0;
        var cut = Render<MusselSheetView>(p => p.Add(x => x.Sheet, sheet).Add(x => x.CellCommitted, _ => committed++));
        var bridge = new SheetScriptBridge(cut.Instance);
        await cut.InvokeAsync(() => cut.Instance.DisposeAsync().AsTask());

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cut.InvokeAsync(() => bridge.CommitAsync(0, 0, "5", true, null)));
        await cut.InvokeAsync(() => bridge.OnActiveCellChangedAsync(0, 0, 0, 0, 0, 0));

        Assert.Null(sheet.GetValue(0, 0));
        Assert.Equal(0, committed);
    }

    [Fact]
    public async Task CommitsApplyInTheOrderTheyArrive()
    {
        var sheet = new Sheet(2, 1);
        var held = new TaskCompletionSource();
        var order = new List<int>();
        var cut = Render<MusselSheetView>(p => p
            .Add(x => x.Sheet, sheet)
            .Add(x => x.CellCommitting, e => e.Address.Row == 0 ? held.Task : Task.CompletedTask)
            .Add(x => x.CellCommitted, e => order.Add(e.Address.Row)));
        var bridge = new SheetScriptBridge(cut.Instance);
        Task[] commits = [];

        await cut.InvokeAsync(() =>
        {
            commits = [bridge.CommitAsync(0, 0, "1", true, null), bridge.CommitAsync(1, 0, "2", true, null)];
        });
        await cut.InvokeAsync(held.SetResult);
        await Task.WhenAll(commits).WaitAsync(Timeout, Xunit.TestContext.Current.CancellationToken);

        Assert.Equal([0, 1], order);
    }

    [Fact]
    public async Task TheEditTextWaitsForTheCommitBeforeIt()
    {
        var sheet = new Sheet(1, 1);
        var held = new TaskCompletionSource();
        var cut = Render<MusselSheetView>(p => p.Add(x => x.Sheet, sheet).Add(x => x.CellCommitting, _ => held.Task));
        var bridge = new SheetScriptBridge(cut.Instance);
        Task? commit = null;
        Task<string>? text = null;

        await cut.InvokeAsync(() =>
        {
            commit = bridge.CommitAsync(0, 0, "5", true, null);
            text = bridge.GetEditTextAsync(0, 0);
        });
        Assert.False(text!.IsCompleted);

        await cut.InvokeAsync(held.SetResult);
        await commit!.WaitAsync(Timeout, Xunit.TestContext.Current.CancellationToken);

        Assert.Equal("5", await text.WaitAsync(Timeout, Xunit.TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task AHandlerCanAwaitAnUndo()
    {
        var sheet = new Sheet(1, 1);
        var cut = Render<MusselSheetView>(p => p.Add(x => x.Sheet, sheet));
        cut.Render(p => p.Add(x => x.CellCommitted, async e =>
        {
            if (e.Reason == CellChangeReason.Input)
            {
                await cut.Instance.UndoAsync().ConfigureAwait(true);
            }
        }));
        var bridge = new SheetScriptBridge(cut.Instance);
        Task? commit = null;

        await cut.InvokeAsync(() =>
        {
            commit = bridge.CommitAsync(0, 0, "5", true, null);
        });
        await commit!.WaitAsync(Timeout, Xunit.TestContext.Current.CancellationToken);

        Assert.Null(sheet.GetValue(0, 0));
    }

    private static void InsertOnce(Sheet sheet, ref bool inserted)
    {
        if (!inserted)
        {
            inserted = true;
            sheet.InsertRows(0, 1);
        }
    }
}
