namespace MusselSheet.E2ETests;

public sealed class SamplePage(IBrowserContext context, IPage page) : IAsyncDisposable
{
    public IPage Page => page;

    public IKeyboard Keyboard => page.Keyboard;

    public ILocator Grid => page.Locator("[role=grid]");

    public ILocator ActiveCell => page.Locator(".sample-active");

    public ILocator Selection => page.Locator(".sample-selection");

    public ILocator Changed => page.Locator(".sample-changed");

    public ILocator Note => page.Locator(".mussel-sheet-note");

    public ILocator Log => page.Locator(".sample-log li");

    public ILocator Cell(int row, int column) => page.Locator($".mussel-sheet-row[data-r='{row}'] .mussel-sheet-cell[data-c='{column}']");

    public ILocator ColumnHeader(int column) => page.Locator($".mussel-sheet-colhead-cell[data-c='{column}']");

    public ILocator RowHeader(int row) => page.Locator($".mussel-sheet-row[data-r='{row}'] > .mussel-sheet-rowhead");

    public ILocator Menu => page.Locator(".mussel-sheet-menu");

    public ILocator MenuItem(string action) => page.Locator($".mussel-sheet-menu-item[data-action='{action}']");

    public static Task RightClickAsync(ILocator target) => target.ClickAsync(new() { Button = MouseButton.Right });

    public Task WaitForSheetAsync() =>
        page.Locator(".mussel-sheet-sink").WaitForAsync(new() { State = WaitForSelectorState.Attached, Timeout = 30_000 });

    public static Task<int[]> RectAsync(ILocator target) =>
        target.EvaluateAsync<int[]>("e => { const b = e.getBoundingClientRect(); return [b.x, b.y, b.width, b.height].map(Math.round); }");

    public Task ScrollSheetAsync(int left, int top) =>
        page.EvaluateAsync("([left, top]) => { const v = document.querySelector('.mussel-sheet-viewport'); v.scrollLeft = left; v.scrollTop = top; }", new[] { left, top });

    public Task<int> SheetScrollTopAsync() => page.EvaluateAsync<int>("() => Math.round(document.querySelector('.mussel-sheet-viewport').scrollTop)");

    public Task FakeAsyncClipboardAsync() =>
        page.EvaluateAsync(
            """
            () => {
                window.fakeClipboard = { text: '', deny: false };
                const fake = {
                    write: async items => {
                        const blob = await items[0].getType('text/plain');
                        window.fakeClipboard.text = await blob.text();
                    },
                    writeText: async text => {
                        window.fakeClipboard.text = text;
                    },
                    readText: async () => {
                        if (window.fakeClipboard.deny) {
                            throw new DOMException('denied', 'NotAllowedError');
                        }
                        return window.fakeClipboard.text;
                    },
                };
                Object.defineProperty(navigator, 'clipboard', { value: fake, configurable: true });
            }
            """);

    public Task WaitForClipboardAsync(string text) =>
        page.WaitForFunctionAsync("text => window.fakeClipboard.text === text", text);

    public Task<string> DispatchClipboardAsync(string type, string? text) =>
        page.EvaluateAsync<string>(
            """
            ([type, text]) => {
                const data = new DataTransfer();
                if (text !== null) {
                    data.setData('text/plain', text);
                }
                document.querySelector('.mussel-sheet-sink').dispatchEvent(new ClipboardEvent(type, { clipboardData: data, bubbles: true, cancelable: true }));
                return data.getData('text/plain');
            }
            """,
            new object?[] { type, text });

    public ValueTask DisposeAsync() => context.DisposeAsync();
}
