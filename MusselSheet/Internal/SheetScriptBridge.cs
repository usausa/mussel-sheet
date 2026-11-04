namespace MusselSheet.Internal;

using System.Text.Json;

using Microsoft.JSInterop;

// The only object the script can call, so that these entry points stay out of the public API of the view
internal sealed class SheetScriptBridge
{
    private readonly MusselSheetView view;

    public SheetScriptBridge(MusselSheetView view)
    {
        this.view = view;
    }

    [JSInvokable]
    public void OnViewportChanged(int top, int height, int left, int width) => view.OnViewportChanged(top, height, left, width);

    [JSInvokable]
    public Task OnMenuItemAsync(string name, int top, int left, int bottom, int right, int row, int column) => view.OnMenuItemAsync(name, top, left, bottom, right, row, column);

    [JSInvokable]
    public async Task<JsonElement> CommitAsync(int row, int column, string text, bool optimistic) => SheetScriptJson.ToElement(await view.CommitAsync(row, column, text, optimistic).ConfigureAwait(true));

    [JSInvokable]
    public async Task<JsonElement> ClearAsync(int row, int column) => SheetScriptJson.ToElement(await view.ClearAsync(row, column).ConfigureAwait(true));

    [JSInvokable]
    public async Task<JsonElement> PasteAsync(int row, int column, string text, bool optimistic) => SheetScriptJson.ToElement(await view.PasteAsync(row, column, text, optimistic).ConfigureAwait(true));

    [JSInvokable]
    public async Task<JsonElement> CutAsync(int row, int column) => SheetScriptJson.ToElement(await view.CutAsync(row, column).ConfigureAwait(true));

    [JSInvokable]
    public async Task<JsonElement> PasteRangeAsync(int top, int left, int bottom, int right, IJSStreamReference data) => SheetScriptJson.ToElement(await view.PasteRangeAsync(top, left, bottom, right, data).ConfigureAwait(true));

    [JSInvokable]
    public Task ClearRangeAsync(int top, int left, int bottom, int right) => view.ClearRangeAsync(top, left, bottom, right);

    [JSInvokable]
    public Task CutRangeAsync(int top, int left, int bottom, int right) => view.CutRangeAsync(top, left, bottom, right);

    [JSInvokable]
    public JsonElement GetRangeText(int top, int left, int bottom, int right) => SheetScriptJson.ToElement(view.GetRangeText(top, left, bottom, right));

    [JSInvokable]
    public async Task<JsonElement> FillAsync(int top, int left, int bottom, int right, string text) => SheetScriptJson.ToElement(await view.FillAsync(top, left, bottom, right, text).ConfigureAwait(true));

    [JSInvokable]
    public async Task<JsonElement> FillDownAsync(int top, int left, int bottom, int right) => SheetScriptJson.ToElement(await view.FillDownAsync(top, left, bottom, right).ConfigureAwait(true));

    [JSInvokable]
    public async Task<JsonElement> FillRightAsync(int top, int left, int bottom, int right) => SheetScriptJson.ToElement(await view.FillRightAsync(top, left, bottom, right).ConfigureAwait(true));

    [JSInvokable]
    public async Task<JsonElement> AutoFillAsync(int top, int left, int bottom, int right, int toTop, int toLeft, int toBottom, int toRight, bool toggle) => SheetScriptJson.ToElement(await view.AutoFillAsync(top, left, bottom, right, toTop, toLeft, toBottom, toRight, toggle).ConfigureAwait(true));

    [JSInvokable]
    public async Task<JsonElement> AutoFillDownAsync(int top, int left, int bottom, int right) => SheetScriptJson.ToElement(await view.AutoFillDownAsync(top, left, bottom, right).ConfigureAwait(true));

    [JSInvokable]
    public int[] FindEdge(int row, int column, string direction) => view.FindEdge(row, column, direction);

    [JSInvokable]
    public Task<bool> AddRowFromScriptAsync(int row, int column) => view.AddRowFromScriptAsync(row, column);

    [JSInvokable]
    public Task<int[]?> ChangeStructureAsync(string action, int top, int left, int bottom, int right, int row, int column) => view.ChangeStructureAsync(action, top, left, bottom, right, row, column);

    [JSInvokable]
    public async Task<JsonElement> ToggleAsync(int row, int column) => SheetScriptJson.ToElement(await view.ToggleAsync(row, column).ConfigureAwait(true));

    [JSInvokable]
    public Task<int[]?> UndoFromScriptAsync(bool redo) => view.UndoFromScriptAsync(redo);

    [JSInvokable]
    public Task OnActiveCellChangedAsync(int row, int column, int top, int left, int bottom, int right) => view.OnActiveCellChangedAsync(row, column, top, left, bottom, right);

    [JSInvokable]
    public string GetEditText(int row, int column) => view.GetEditText(row, column);

    [JSInvokable]
    public async Task<string[]?> GetSuggestionsAsync(int row, int column, string text) => (await view.GetSuggestionsAsync(row, column, text).ConfigureAwait(true))?.ToArray();

    [JSInvokable]
    public bool OpenCustomEditor(int row, int column) => view.OpenCustomEditor(row, column);

    [JSInvokable]
    public async Task<JsonElement> CommitCustomEditorAsync() => SheetScriptJson.ToElement(await view.CommitCustomEditorAsync().ConfigureAwait(true));

    [JSInvokable]
    public void CancelCustomEditor() => view.CancelCustomEditor();
}
