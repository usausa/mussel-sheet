namespace MusselSheet;

using Microsoft.JSInterop;

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
            commit = bridge.CommitAsync(0, 0, "5", true, null, cut.Instance.SheetGeneration);
            undo = bridge.UndoFromScriptAsync(false, cut.Instance.SheetGeneration);
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
            calls = [bridge.CommitAsync(0, 0, "5", true, null, cut.Instance.SheetGeneration), bridge.CommitCustomEditorAsync(false), bridge.CancelCustomEditorAsync()];
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
            calls = [bridge.CommitAsync(0, 0, "5", true, null, cut.Instance.SheetGeneration), bridge.CommitCustomEditorAsync(false), edit.CancelAsync()];
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

        await cut.InvokeAsync(() => bridge.CommitAsync(1, 0, "x", true, null, cut.Instance.SheetGeneration));

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
            commit = a.CommitAsync(0, 0, "5", true, null, first.Instance.SheetGeneration);
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
            waiting = a.CommitAsync(0, 0, "5", true, null, first.Instance.SheetGeneration);
            queued = b.CommitAsync(1, 0, "7", true, null, second.Instance.SheetGeneration);
        });
        await second.InvokeAsync(() => second.Instance.DisposeAsync().AsTask());
        await first.InvokeAsync(held.SetResult);
        await waiting!.WaitAsync(Timeout, Xunit.TestContext.Current.CancellationToken);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => queued!);
        Assert.Null(sheet.GetValue(1, 0));
    }

    [Fact]
    public async Task ARedoThatWaitsWhileItsViewIsDisposedDoesNothing()
    {
        var sheet = new Sheet(2, 1);
        var held = new TaskCompletionSource();
        var first = Render<MusselSheetView>(p => p.Add(x => x.Sheet, sheet).Add(x => x.CellCommitting, _ => held.Task));
        var second = Render<MusselSheetView>(p => p.Add(x => x.Sheet, sheet));
        var a = new SheetScriptBridge(first.Instance);
        var b = new SheetScriptBridge(second.Instance);
        await second.InvokeAsync(() => b.CommitAsync(0, 0, "5", false, null, second.Instance.SheetGeneration));
        await second.InvokeAsync(second.Instance.UndoAsync);
        Task? waiting = null;
        Task<bool>? queued = null;

        await first.InvokeAsync(() =>
        {
            waiting = a.CommitAsync(1, 0, "7", true, null, first.Instance.SheetGeneration);
            queued = second.Instance.RedoAsync();
        });
        await second.InvokeAsync(() => second.Instance.DisposeAsync().AsTask());
        await first.InvokeAsync(held.SetResult);
        await waiting!.WaitAsync(Timeout, Xunit.TestContext.Current.CancellationToken);

        Assert.False(await queued!.WaitAsync(Timeout, Xunit.TestContext.Current.CancellationToken));
        Assert.Null(sheet.GetValue(0, 0));
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
        await second.InvokeAsync(() => b.CommitAsync(0, 0, "5", false, null, second.Instance.SheetGeneration));
        var stored = sheet.GetValue(0, 0);
        Task? waiting = null;
        Task<bool>? queued = null;

        await first.InvokeAsync(() =>
        {
            waiting = a.CommitAsync(1, 0, "7", true, null, first.Instance.SheetGeneration);
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
            waiting = a.CommitAsync(0, 0, "5", true, null, first.Instance.SheetGeneration);
            queued = edit.CommitAsync();
        });
        await second.InvokeAsync(() => second.Instance.DisposeAsync().AsTask());
        await first.InvokeAsync(held.SetResult);
        await waiting!.WaitAsync(Timeout, Xunit.TestContext.Current.CancellationToken);

        Assert.False(await queued!.WaitAsync(Timeout, Xunit.TestContext.Current.CancellationToken));
        Assert.Null(sheet.GetValue(1, 0));
    }

    [Fact]
    public async Task ATemplateCommitOnAnotherSheetWritesNothing()
    {
        var sheet = new Sheet(2, 1);
        sheet["A2"].Configure(c => c.Kind = CellKind.Custom);
        var other = new Sheet(2, 1);
        other["A2"].Configure(c => c.Kind = CellKind.Custom);
        CellEditContext? edit = null;
        var cut = Render<MusselSheetView>(p => p
            .Add(x => x.Sheet, sheet)
            .Add(x => x.CustomEditor, context =>
            {
                edit = context;
                return static _ => { };
            }));
        await cut.InvokeAsync(() => new SheetScriptBridge(cut.Instance).OpenCustomEditorAsync(1, 0));
        cut.Render(p => p.Add(x => x.Sheet, other));
        edit!.Value = "red";

        var committed = await cut.InvokeAsync(edit.CommitAsync);

        Assert.False(committed);
        Assert.Null(other.GetValue(1, 0));
        Assert.Null(sheet.GetValue(1, 0));
    }

    [Fact]
    public async Task ACommitTypedBeforeTheSheetWasReplacedWritesNothing()
    {
        var sheet = new Sheet(2, 1);
        var other = new Sheet(2, 1);
        var cut = Render<MusselSheetView>(p => p.Add(x => x.Sheet, sheet));
        var bridge = new SheetScriptBridge(cut.Instance);
        var generation = cut.Instance.SheetGeneration;
        cut.Render(p => p.Add(x => x.Sheet, other));

        var result = await cut.InvokeAsync(() => bridge.CommitAsync(1, 0, "abc", false, null, generation));

        Assert.False(result.GetProperty("stored").GetBoolean());
        Assert.Null(other.GetValue(1, 0));
        Assert.Null(sheet.GetValue(1, 0));
        Assert.Equal(cut.Instance.SheetGeneration, JSInterop.Invocations["setMap"][^1].Arguments[2]);
    }

    [Fact]
    public async Task WritesSentBeforeTheSheetWasReplacedChangeNothing()
    {
        var sheet = new Sheet(3, 2);
        var other = new Sheet(3, 2);
        other.Range("B1:B3").Configure(static c => c.Kind = CellKind.Checkbox);
        other.SetValues(0, 0, [["a", true], ["b", false], [null, true]]);
        var cut = Render<MusselSheetView>(p => p
            .Add(x => x.Sheet, sheet)
            .Add(x => x.AllowInsertRows, true)
            .Add(x => x.AllowDeleteRows, true)
            .Add(x => x.LastRowEnterMode, SheetLastRowEnterMode.AddRow));
        var bridge = new SheetScriptBridge(cut.Instance);
        var generation = cut.Instance.SheetGeneration;
        cut.Render(p => p.Add(x => x.Sheet, other));
        var data = new TextStream("q");
        try
        {
            await cut.InvokeAsync(async () =>
            {
                await cut.Instance.CommitAsync(0, 0, "z", false).ConfigureAwait(true);
                await bridge.ClearAsync(0, 0, generation).ConfigureAwait(true);
                await bridge.PasteAsync(1, 0, "x", true, null, generation).ConfigureAwait(true);
                await bridge.CutAsync(1, 0, generation).ConfigureAwait(true);
                await bridge.ToggleAsync(0, 1, generation).ConfigureAwait(true);
                await bridge.ClearRangeAsync(0, 0, 2, 1, generation).ConfigureAwait(true);
                await bridge.CutRangeAsync(0, 0, 2, 1, generation).ConfigureAwait(true);
                await bridge.PasteRangeAsync(0, 0, 0, 0, data, generation).ConfigureAwait(true);
                await bridge.FillAsync(0, 0, 2, 0, "y", null, generation).ConfigureAwait(true);
                await bridge.FillDownAsync(0, 0, 2, 0, generation).ConfigureAwait(true);
                await bridge.FillRightAsync(0, 0, 0, 1, generation).ConfigureAwait(true);
                await bridge.AutoFillAsync(0, 0, 0, 0, 0, 0, 2, 0, false, generation).ConfigureAwait(true);
                await bridge.AutoFillDownAsync(1, 0, 1, 0, generation).ConfigureAwait(true);
                await bridge.ChangeStructureAsync("deleteRows", 0, 0, 0, 1, 0, 0, generation).ConfigureAwait(true);
                await bridge.AddRowFromScriptAsync(2, 0, generation).ConfigureAwait(true);
                await bridge.UndoFromScriptAsync(false, generation).ConfigureAwait(true);
            });
        }
        finally
        {
            await data.DisposeAsync();
        }

        object?[][] values = [["z", true], ["b", false], [null, true]];
        Assert.Equal(values, other.Range("A1:B3").GetValues());
        Assert.Equal(3, other.RowCount);
        Assert.Equal(cut.Instance.SheetGeneration, JSInterop.Invocations["setMap"][^1].Arguments[2]);
    }

    [Fact]
    public async Task ACommitFromBeforeTheReplacementIsAnsweredAfterTheNewMap()
    {
        var module = JSInterop.SetupModule("./_content/MusselSheet/MusselSheet.js");
        var controller = module.SetupModule("attach", static _ => true);
        var sheet = new Sheet(1, 1);
        var other = new Sheet(1, 1) { Culture = CultureInfo.GetCultureInfo("en-US") };
        var cut = Render<MusselSheetView>(p => p.Add(x => x.Sheet, sheet));
        var bridge = new SheetScriptBridge(cut.Instance);
        var generation = cut.Instance.SheetGeneration;
        var language = controller.SetupVoid("setLanguage", static _ => true);
        cut.Render(p => p.Add(x => x.Sheet, other));

        var sent = await cut.InvokeAsync(async () =>
        {
            await bridge.CommitAsync(0, 0, "a", false, null, generation).ConfigureAwait(true);
            return controller.Invocations["setMap"].Select(static x => x.Arguments[2]).ToArray();
        });
        await cut.InvokeAsync(language.SetVoidResult);

        Assert.Equal(cut.Instance.SheetGeneration, Assert.Single(sent));
        Assert.Null(other.GetValue(0, 0));
    }

    [Fact]
    public async Task AMenuItemChosenBeforeTheReplacementIsNotRaisedAndTheNewMapIsSentFirst()
    {
        var module = JSInterop.SetupModule("./_content/MusselSheet/MusselSheet.js");
        var controller = module.SetupModule("attach", static _ => true);
        var sheet = new Sheet(1, 1);
        var other = new Sheet(1, 1) { Culture = CultureInfo.GetCultureInfo("en-US") };
        var clicked = new List<Sheet>();
        var cut = Render<MusselSheetView>(p => p.Add(x => x.Sheet, sheet).Add(x => x.MenuItemClicked, e => clicked.Add(e.Sheet)));
        var bridge = new SheetScriptBridge(cut.Instance);
        var generation = cut.Instance.SheetGeneration;
        var language = controller.SetupVoid("setLanguage", static _ => true);
        cut.Render(p => p.Add(x => x.Sheet, other));

        var sent = await cut.InvokeAsync(async () =>
        {
            await bridge.OnMenuItemAsync("note", 0, 0, 0, 0, 0, 0, generation).ConfigureAwait(true);
            return controller.Invocations["setMap"].Select(static x => x.Arguments[2]).ToArray();
        });
        await cut.InvokeAsync(language.SetVoidResult);
        await cut.InvokeAsync(() => bridge.OnMenuItemAsync("note", 0, 0, 0, 0, 0, 0, cut.Instance.SheetGeneration));

        Assert.Equal(cut.Instance.SheetGeneration, Assert.Single(sent));
        Assert.Same(other, Assert.Single(clicked));
    }

    [Fact]
    public async Task LongTextsSentAfterTheViewIsGoneAreReleasedUnread()
    {
        var sheet = new Sheet(1, 1);
        var cut = Render<MusselSheetView>(p => p.Add(x => x.Sheet, sheet));
        var bridge = new SheetScriptBridge(cut.Instance);
        var generation = cut.Instance.SheetGeneration;
        await cut.InvokeAsync(() => cut.Instance.DisposeAsync().AsTask());

        var released = await cut.InvokeAsync(async () => new[]
        {
            await Canceled(x => bridge.CommitAsync(0, 0, null, false, x, generation)).ConfigureAwait(true),
            await Canceled(x => bridge.PasteAsync(0, 0, null, false, x, generation)).ConfigureAwait(true),
            await Canceled(x => bridge.FillAsync(0, 0, 0, 0, null, x, generation)).ConfigureAwait(true),
            await Canceled(x => bridge.PasteRangeAsync(0, 0, 0, 0, x, generation)).ConfigureAwait(true)
        });

        Assert.All(released, Assert.True);
        Assert.Null(sheet.GetValue(0, 0));

        static async Task<bool> Canceled(Func<IJSStreamReference, Task> send) =>
            await Released(async x => await Assert.ThrowsAnyAsync<OperationCanceledException>(() => send(x)).ConfigureAwait(true)).ConfigureAwait(true);
    }

    [Fact]
    public async Task ALongTextThatWaitsWhileItsViewIsDisposedIsReleasedUnread()
    {
        var sheet = new Sheet(2, 1);
        var held = new TaskCompletionSource();
        var first = Render<MusselSheetView>(p => p.Add(x => x.Sheet, sheet).Add(x => x.CellCommitting, _ => held.Task));
        var second = Render<MusselSheetView>(p => p.Add(x => x.Sheet, sheet));
        var a = new SheetScriptBridge(first.Instance);
        var b = new SheetScriptBridge(second.Instance);
        var data = new TextStream("7");
        try
        {
            Task? waiting = null;
            Task? queued = null;
            await first.InvokeAsync(() =>
            {
                waiting = a.CommitAsync(0, 0, "5", true, null, first.Instance.SheetGeneration);
                queued = b.CommitAsync(1, 0, null, true, data, second.Instance.SheetGeneration);
            });
            await second.InvokeAsync(() => second.Instance.DisposeAsync().AsTask());
            await first.InvokeAsync(held.SetResult);
            await waiting!.WaitAsync(Timeout, Xunit.TestContext.Current.CancellationToken);

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => queued!);
            Assert.True(data.Disposed);
            Assert.Null(sheet.GetValue(1, 0));
        }
        finally
        {
            await data.DisposeAsync();
        }
    }

    [Fact]
    public async Task LongTextsSentBeforeTheSheetWasReplacedAreReleasedUnread()
    {
        var sheet = new Sheet(1, 1);
        var other = new Sheet(1, 1);
        var cut = Render<MusselSheetView>(p => p.Add(x => x.Sheet, sheet));
        var bridge = new SheetScriptBridge(cut.Instance);
        var generation = cut.Instance.SheetGeneration;
        cut.Render(p => p.Add(x => x.Sheet, other));

        var released = await cut.InvokeAsync(async () => new[]
        {
            await Released(x => bridge.CommitAsync(0, 0, null, false, x, generation)).ConfigureAwait(true),
            await Released(x => bridge.PasteAsync(0, 0, null, false, x, generation)).ConfigureAwait(true),
            await Released(x => bridge.FillAsync(0, 0, 0, 0, null, x, generation)).ConfigureAwait(true),
            await Released(x => bridge.PasteRangeAsync(0, 0, 0, 0, x, generation)).ConfigureAwait(true)
        });

        Assert.All(released, Assert.True);
        Assert.Null(other.GetValue(0, 0));
    }

    [Fact]
    public async Task ATemplateCommitForADeletedCellGivesFalse()
    {
        var sheet = new Sheet(2, 1);
        sheet["A2"].Configure(c => c.Kind = CellKind.Custom);
        CellEditContext? edit = null;
        var cut = Render<MusselSheetView>(p => p
            .Add(x => x.Sheet, sheet)
            .Add(x => x.CustomEditor, context =>
            {
                edit = context;
                return static _ => { };
            }));
        await cut.InvokeAsync(() => new SheetScriptBridge(cut.Instance).OpenCustomEditorAsync(1, 0));
        await cut.InvokeAsync(() => sheet.DeleteRows(1, 1));
        edit!.Value = "red";

        var committed = await cut.InvokeAsync(edit.CommitAsync);

        Assert.False(committed);
        Assert.Equal(1, sheet.RowCount);
    }

    [Fact]
    public async Task TheGateOpensAgainAfterAHandlerThrows()
    {
        var sheet = new Sheet(2, 1);
        var cut = Render<MusselSheetView>(p => p.Add(x => x.Sheet, sheet).Add(x => x.CellCommitting, e => e.Address.Row == 0 ? throw new InvalidOperationException("app") : Task.CompletedTask));
        var bridge = new SheetScriptBridge(cut.Instance);

        await Assert.ThrowsAsync<InvalidOperationException>(() => cut.InvokeAsync(() => bridge.CommitAsync(0, 0, "1", true, null, cut.Instance.SheetGeneration)));
        await cut.InvokeAsync(() => bridge.CommitAsync(1, 0, "2", true, null, cut.Instance.SheetGeneration)).WaitAsync(Timeout, Xunit.TestContext.Current.CancellationToken);

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

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cut.InvokeAsync(() => bridge.CommitAsync(0, 0, "5", true, null, cut.Instance.SheetGeneration)));
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
            commits = [bridge.CommitAsync(0, 0, "1", true, null, cut.Instance.SheetGeneration), bridge.CommitAsync(1, 0, "2", true, null, cut.Instance.SheetGeneration)];
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
            commit = bridge.CommitAsync(0, 0, "5", true, null, cut.Instance.SheetGeneration);
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
            commit = bridge.CommitAsync(0, 0, "5", true, null, cut.Instance.SheetGeneration);
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

    private static async Task<bool> Released(Func<IJSStreamReference, Task> send)
    {
        var data = new TextStream("a");
        try
        {
            await send(data).ConfigureAwait(true);
            return data.Disposed;
        }
        finally
        {
            await data.DisposeAsync().ConfigureAwait(true);
        }
    }
}
