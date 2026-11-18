namespace MusselSheet.Internal;

using System.Text.Json;

using Microsoft.JSInterop;

// The only object the script can call, so that these entry points stay out of the public API of the view
internal sealed class SheetScriptBridge(MusselSheetView view)
{
    [JSInvokable]
    public void OnViewportChanged(int top, int height, int left, int width)
    {
        if (!view.IsDisposed)
        {
            view.OnViewportChanged(top, height, left, width);
        }
    }

    [JSInvokable]
    public Task OnMenuItemAsync(string name, int top, int left, int bottom, int right, int row, int column) =>
        RunAsync(() => view.OnMenuItemAsync(name, top, left, bottom, right, row, column));

    [JSInvokable]
    public Task<JsonElement> CommitAsync(int row, int column, string? text, bool optimistic, IJSStreamReference? data) =>
        RunAsync(async () =>
        {
            var value = text ?? await MusselSheetView.ReadTextAsync(data).ConfigureAwait(true);
            return SheetScriptJson.ToElement(value is null ? view.RefuseLargeText(row, column, optimistic) : await view.CommitAsync(row, column, value, optimistic).ConfigureAwait(true));
        });

    [JSInvokable]
    public Task<JsonElement> ClearAsync(int row, int column) =>
        RunAsync(async () => SheetScriptJson.ToElement(await view.ClearAsync(row, column).ConfigureAwait(true)));

    [JSInvokable]
    public Task<JsonElement> PasteAsync(int row, int column, string? text, bool optimistic, IJSStreamReference? data) =>
        RunAsync(async () =>
        {
            var value = text ?? await MusselSheetView.ReadTextAsync(data).ConfigureAwait(true);
            return SheetScriptJson.ToElement(value is null ? view.RefuseLargeText(row, column, optimistic) : await view.PasteAsync(row, column, value, optimistic).ConfigureAwait(true));
        });

    [JSInvokable]
    public Task<JsonElement> CutAsync(int row, int column) =>
        RunAsync(async () => SheetScriptJson.ToElement(await view.CutAsync(row, column).ConfigureAwait(true)));

    [JSInvokable]
    public Task<JsonElement> PasteRangeAsync(int top, int left, int bottom, int right, IJSStreamReference data) =>
        RunAsync(async () => SheetScriptJson.ToElement(await view.PasteRangeAsync(top, left, bottom, right, data).ConfigureAwait(true)));

    [JSInvokable]
    public Task ClearRangeAsync(int top, int left, int bottom, int right) => RunAsync(() => view.ClearRangeAsync(top, left, bottom, right));

    [JSInvokable]
    public Task CutRangeAsync(int top, int left, int bottom, int right) => RunAsync(() => view.CutRangeAsync(top, left, bottom, right));

    [JSInvokable]
    public Task<JsonElement> GetRangeTextAsync(int top, int left, int bottom, int right) =>
        RunAsync(() => Task.FromResult(SheetScriptJson.ToElement(view.GetRangeText(top, left, bottom, right))));

    [JSInvokable]
    public Task<JsonElement> FillAsync(int top, int left, int bottom, int right, string? text, IJSStreamReference? data) =>
        RunAsync(async () =>
        {
            var value = text ?? await MusselSheetView.ReadTextAsync(data).ConfigureAwait(true);
            return SheetScriptJson.ToElement(value is null ? view.RefuseLargeText(top, left, bottom, right) : await view.FillAsync(top, left, bottom, right, value).ConfigureAwait(true));
        });

    [JSInvokable]
    public Task<JsonElement> FillDownAsync(int top, int left, int bottom, int right) =>
        RunAsync(async () => SheetScriptJson.ToElement(await view.FillDownAsync(top, left, bottom, right).ConfigureAwait(true)));

    [JSInvokable]
    public Task<JsonElement> FillRightAsync(int top, int left, int bottom, int right) =>
        RunAsync(async () => SheetScriptJson.ToElement(await view.FillRightAsync(top, left, bottom, right).ConfigureAwait(true)));

    [JSInvokable]
    public Task<JsonElement> AutoFillAsync(int top, int left, int bottom, int right, int toTop, int toLeft, int toBottom, int toRight, bool toggle) =>
        RunAsync(async () => SheetScriptJson.ToElement(await view.AutoFillAsync(top, left, bottom, right, toTop, toLeft, toBottom, toRight, toggle).ConfigureAwait(true)));

    [JSInvokable]
    public Task<JsonElement> AutoFillDownAsync(int top, int left, int bottom, int right) =>
        RunAsync(async () => SheetScriptJson.ToElement(await view.AutoFillDownAsync(top, left, bottom, right).ConfigureAwait(true)));

    [JSInvokable]
    public Task<int[]> FindEdgeAsync(int row, int column, string direction) => RunAsync(() => Task.FromResult(view.FindEdge(row, column, direction)));

    [JSInvokable]
    public Task<bool> AddRowFromScriptAsync(int row, int column) => RunAsync(() => view.AddRowFromScriptAsync(row, column));

    [JSInvokable]
    public Task<int[]?> ChangeStructureAsync(string action, int top, int left, int bottom, int right, int row, int column) =>
        RunAsync(() => view.ChangeStructureAsync(action, top, left, bottom, right, row, column));

    [JSInvokable]
    public Task<JsonElement> ToggleAsync(int row, int column) =>
        RunAsync(async () => SheetScriptJson.ToElement(await view.ToggleAsync(row, column).ConfigureAwait(true)));

    [JSInvokable]
    public Task<int[]?> UndoFromScriptAsync(bool redo) => RunAsync(() => view.UndoFromScriptAsync(redo));

    [JSInvokable]
    public Task OnActiveCellChangedAsync(int row, int column, int top, int left, int bottom, int right) =>
        view.IsDisposed ? Task.CompletedTask : view.ReportAsync(view.OnActiveCellChangedAsync(row, column, top, left, bottom, right));

    [JSInvokable]
    public Task<string> GetEditTextAsync(int row, int column) => RunAsync(() => Task.FromResult(view.GetEditText(row, column)));

    [JSInvokable]
    public Task<string[]?> GetSuggestionsAsync(int row, int column, string text) =>
        view.IsDisposed ? Task.FromCanceled<string[]?>(new CancellationToken(true)) : SuggestionsAsync(row, column, text);

    [JSInvokable]
    public Task<bool> OpenCustomEditorAsync(int row, int column) => RunAsync(() => Task.FromResult(view.OpenCustomEditor(row, column)));

    [JSInvokable]
    public Task<JsonElement> CommitCustomEditorAsync(bool optimistic) =>
        RunAsync(async () => SheetScriptJson.ToElement(await view.CommitCustomEditorAsync(optimistic).ConfigureAwait(true)));

    [JSInvokable]
    public Task CancelCustomEditorAsync() =>
        RunAsync(() =>
        {
            view.CancelCustomEditor();
            return Task.CompletedTask;
        });

    private async Task<string[]?> SuggestionsAsync(int row, int column, string text) =>
        (await view.ReportAsync(view.GetSuggestionsAsync(row, column, text)).ConfigureAwait(true))?.ToArray();

    // Calls that arrive after the view is disposed, or wait in line until then, do nothing: those that return a value are canceled
    private Task<T> RunAsync<T>(Func<Task<T>> operation) =>
        view.ReportAsync(view.RunInTurnAsync(() => view.IsDisposed ? Task.FromCanceled<T>(new CancellationToken(true)) : operation()));

    private Task RunAsync(Func<Task> operation) =>
        view.ReportAsync(view.RunInTurnAsync(() => view.IsDisposed ? Task.CompletedTask : operation()));
}
