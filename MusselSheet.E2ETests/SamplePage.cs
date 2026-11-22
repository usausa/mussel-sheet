namespace MusselSheet.E2ETests;

using System.Collections.Concurrent;

public sealed class SamplePage(IBrowserContext context, IPage page, Func<string> appLog) : IAsyncDisposable
{
    private readonly ConcurrentQueue<string> events = new();

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

    public void Watch()
    {
        page.Console += (_, m) => events.Enqueue($"{DateTime.Now:HH:mm:ss.fff} console {m.Type}: {m.Text}");
        page.PageError += (_, e) => events.Enqueue($"{DateTime.Now:HH:mm:ss.fff} pageerror: {e}");
        page.RequestFailed += (_, r) => events.Enqueue($"{DateTime.Now:HH:mm:ss.fff} requestfailed: {r.Url} {r.Failure}");
        page.FrameNavigated += (_, f) => events.Enqueue($"{DateTime.Now:HH:mm:ss.fff} navigated: {f.Url}");
    }

    public Task WaitForSheetAsync() =>
        page.WaitForFunctionAsync(
            "() => { const roots = document.querySelectorAll('.mussel-sheet'); return (roots.length > 0) && [...roots].every((root) => root.hasAttribute('data-attached')); }",
            null,
            new() { Timeout = 30_000 });

    public static Task<int[]> RectAsync(ILocator target) =>
        target.EvaluateAsync<int[]>("e => { const b = e.getBoundingClientRect(); return [b.x, b.y, b.width, b.height].map(Math.round); }");

    public Task ScrollSheetAsync(int left, int top) =>
        page.EvaluateAsync("([left, top]) => { const v = document.querySelector('.mussel-sheet-viewport'); v.scrollLeft = left; v.scrollTop = top; }", new[] { left, top });

    public Task<int> SheetScrollTopAsync() => page.EvaluateAsync<int>("() => Math.round(document.querySelector('.mussel-sheet-viewport').scrollTop)");

    public Task FakeAsyncClipboardAsync() =>
        page.EvaluateAsync(
            """
            () => {
                window.fakeClipboard = { text: '', deny: false, denyWrite: false };
                const refuse = () => {
                    if (window.fakeClipboard.denyWrite) {
                        throw new DOMException('denied', 'NotAllowedError');
                    }
                };
                const fake = {
                    write: async items => {
                        refuse();
                        const blob = await items[0].getType('text/plain');
                        window.fakeClipboard.text = await blob.text();
                    },
                    writeText: async text => {
                        refuse();
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

    public Task HoldCommitsAsync(string method = "CommitAsync") =>
        page.EvaluateAsync(
            """
            method => {
                const proto = DotNet.DotNetObject.prototype;
                const invoke = proto.invokeMethodAsync;
                let target = null;
                let tail = Promise.resolve();
                window.heldCommits = [];
                proto.invokeMethodAsync = function (name, ...args) {
                    if (name === method) {
                        target = this;
                    }
                    if (this !== target) {
                        return invoke.call(this, name, ...args);
                    }
                    const held = name === method ? new Promise(resolve => window.heldCommits.push(resolve)) : null;
                    const result = Promise.all([tail, held]).then(() => invoke.call(this, name, ...args));
                    tail = result.catch(() => { });
                    return result;
                };
            }
            """,
            method);

    public Task AttachScriptAsync(int rows, string[] kinds, string commitMode = "optimistic") =>
        page.EvaluateAsync(
            """
            async ([rows, kinds, commitMode]) => {
                const { attach } = await import(new URL('_content/MusselSheet/MusselSheet.js', document.baseURI).href);
                const root = document.createElement('div');
                root.className = 'script-harness';
                root.innerHTML = '<div class="mussel-sheet-viewport"><div class="mussel-sheet-content"><div class="mussel-sheet-top"></div></div><div class="mussel-sheet-layer"></div></div>';
                document.body.prepend(root);
                const map = count => ({
                    rows: count,
                    columns: kinds.length,
                    headerHeight: 24,
                    rowHeaderWidth: 40,
                    columnWidths: kinds.map(() => 100),
                    defaultRowHeight: 24,
                    customRowHeights: [],
                    rules: kinds.map(kind => ({ kind, disabled: false, policy: 'stay' })),
                    patterns: count > 0 ? [kinds.map((_, index) => index)] : [],
                    rowPatterns: new Array(count).fill(0),
                    merges: [],
                });
                const script = { root, map, calls: [], held: [], errors: [] };
                window.addEventListener('error', e => script.errors.push(e.message));
                script.callsOf = name => script.calls.filter(x => x[0] === name).map(x => x.slice(1));
                script.answer = (name, value) => {
                    const index = script.held.findIndex(x => x.name === name);
                    if (index >= 0) {
                        script.held.splice(index, 1)[0].resolve(value);
                    }
                };
                const dotnet = {
                    invokeMethodAsync: (name, ...args) => {
                        script.calls.push([name, ...args]);
                        return new Promise(resolve => script.held.push({ name, resolve }));
                    },
                };
                script.controller = attach(root, dotnet, { commitMode, customEditor: true }, map(rows), {}, 1);
                script.sink = root.querySelector('.mussel-sheet-sink');
                window.script = script;
            }
            """,
            new object[] { rows, kinds, commitMode });

    public Task RemoveAsyncClipboardAsync() =>
        page.EvaluateAsync("() => Object.defineProperty(navigator, 'clipboard', { value: undefined, configurable: true })");

    public Task RecordCallsAsync() =>
        page.EvaluateAsync(
            """
            () => {
                const proto = DotNet.DotNetObject.prototype;
                const invoke = proto.invokeMethodAsync;
                window.calls = [];
                proto.invokeMethodAsync = function (name, ...args) {
                    window.calls.push(name);
                    return invoke.call(this, name, ...args);
                };
            }
            """);

    public Task<string[]> CallsAsync() => page.EvaluateAsync<string[]>("() => window.calls.splice(0)");

    public Task WaitForCallAsync(string name) => page.WaitForFunctionAsync("name => window.calls.includes(name)", name);

    public Task WaitForHeldCommitsAsync(int count) =>
        page.WaitForFunctionAsync("count => window.heldCommits.length >= count", count);

    public Task ReleaseCommitAsync(int index) => page.EvaluateAsync("index => window.heldCommits[index]()", index);

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

    public Task RejectCallsAsync(params string[] names) =>
        page.EvaluateAsync(
            """
            names => {
                const proto = DotNet.DotNetObject.prototype;
                const invoke = proto.invokeMethodAsync;
                window.rejected = [];
                window.unhandled = 0;
                window.addEventListener('unhandledrejection', () => { window.unhandled++; });
                proto.invokeMethodAsync = function (name, ...args) {
                    if (names.includes(name)) {
                        window.rejected.push(name);
                        return Promise.reject(new Error(name));
                    }
                    return invoke.call(this, name, ...args);
                };
            }
            """,
            names);

    public async Task<int> UnhandledRejectionsAsync(int calls)
    {
        await page.WaitForFunctionAsync("calls => window.rejected.length >= calls", calls).ConfigureAwait(true);
        return await page.EvaluateAsync<int>("() => new Promise(resolve => setTimeout(() => resolve(window.unhandled), 300))").ConfigureAwait(true);
    }

    public ValueTask DisposeAsync() => CloseAsync(TestContext.Current.TestState?.Result == TestResult.Failed);

    public async ValueTask CloseAsync(bool failed)
    {
        try
        {
            if (failed && (Environment.GetEnvironmentVariable("MUSSELSHEET_E2E_DUMP") is { Length: > 0 } folder))
            {
                await DumpAsync(folder).ConfigureAwait(false);
            }
        }
        finally
        {
            await context.DisposeAsync().ConfigureAwait(false);
        }
    }

    private async Task DumpAsync(string folder)
    {
        Directory.CreateDirectory(folder);
        var name = String.Concat((TestContext.Current.Test?.TestDisplayName ?? "test").Select(static x => Char.IsAsciiLetterOrDigit(x) ? x : '_'));
        string state;
        try
        {
            state = await page.EvaluateAsync<string>(
                """
                () => {
                    const root = document.querySelector('.mussel-sheet');
                    const sink = root?.querySelector('.mussel-sheet-sink');
                    const tip = root?.querySelector('.mussel-sheet-tip');
                    const error = document.getElementById('blazor-error-ui');
                    return JSON.stringify({
                        url: location.href,
                        attached: root?.hasAttribute('data-attached'),
                        mode: root?.querySelector('.mussel-sheet-layer')?.dataset.mode,
                        sink: sink?.value,
                        caret: sink ? [sink.selectionStart, sink.selectionEnd] : null,
                        focus: document.activeElement?.className,
                        tip: (tip && !tip.hidden) ? tip.textContent : null,
                        active: document.querySelector('.sample-active')?.textContent,
                        selection: document.querySelector('.sample-selection')?.textContent,
                        rows: root?.querySelectorAll('.mussel-sheet-row').length,
                        log: [...document.querySelectorAll('.sample-log li')].slice(0, 10).map((x) => x.textContent),
                        errorUi: error ? getComputedStyle(error).display : null,
                        reconnect: document.querySelector('#components-reconnect-modal')?.outerHTML?.slice(0, 200) ?? null
                    });
                }
                """).ConfigureAwait(false);
        }
        catch (PlaywrightException e)
        {
            state = e.Message;
        }

        try
        {
            await page.ScreenshotAsync(new() { Path = Path.Combine(folder, name + ".png") }).ConfigureAwait(false);
        }
        catch (Exception e) when (e is PlaywrightException or TimeoutException)
        {
            state += $"{Environment.NewLine}screenshot: {e.Message}";
        }

        await File.WriteAllTextAsync(Path.Combine(folder, name + ".txt"), $"{state}{Environment.NewLine}{String.Join(Environment.NewLine, events)}{Environment.NewLine}--- app{Environment.NewLine}{appLog()}").ConfigureAwait(false);
    }
}
