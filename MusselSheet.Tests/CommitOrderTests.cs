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
}
