namespace MusselSheet.E2ETests;

using Microsoft.Playwright;

using static Microsoft.Playwright.Assertions;

// A real IME cannot be driven, so it is verified manually (the composition events are synthesized here);
// the OS clipboard is never used (clipboard events are synthesized in the page)
public sealed class SheetE2ETests(AppFixture app) : IClassFixture<AppFixture>, IAsyncDisposable
{
    // Set to wasm to run every test on WebAssembly, for example against the published app
    private static readonly string? ForcedMode = Environment.GetEnvironmentVariable("MUSSELSHEET_E2E_MODE");

    private SamplePage? opened;

    public static TheoryData<string> Modes => ["server", "wasm"];

    public async ValueTask DisposeAsync()
    {
        if (opened is not null)
        {
            await opened.DisposeAsync().ConfigureAwait(true);
        }
    }

    [Theory]
    [MemberData(nameof(Modes))]
    public async Task TypingCommitsTheValueAndMovesDown(string mode)
    {
        var sample = await OpenAsync("cell-kinds", mode);

        await sample.Cell(2, 1).ClickAsync();
        await sample.Keyboard.TypeAsync("５０");
        await sample.Keyboard.PressAsync("Enter");

        await Expect(sample.Cell(2, 1)).ToHaveTextAsync("50");
        await Expect(sample.ActiveCell).ToHaveTextAsync("Active cell: B4");

        await sample.Keyboard.TypeAsync("1");
        await sample.Keyboard.PressAsync("Escape");
        await Expect(sample.Cell(3, 1)).ToHaveTextAsync("1,234,567");
    }

    [Theory]
    [MemberData(nameof(Modes))]
    public async Task EnterOnTheLastDetailRowAddsARowThatCanBeUndone(string mode)
    {
        var sample = await OpenAsync("form", mode);
        await Expect(sample.Grid).ToHaveAttributeAsync("aria-rowcount", "10");

        await sample.Cell(7, 1).ClickAsync();
        await sample.Keyboard.TypeAsync("3");
        await sample.Keyboard.PressAsync("Enter");

        await Expect(sample.Grid).ToHaveAttributeAsync("aria-rowcount", "11");
        await Expect(sample.Cell(9, 2)).ToHaveTextAsync("Total");
        await Expect(sample.ActiveCell).ToHaveTextAsync("Active cell: B9");

        await sample.Keyboard.PressAsync("Control+z");
        await Expect(sample.Grid).ToHaveAttributeAsync("aria-rowcount", "10");
        await Expect(sample.ActiveCell).ToHaveTextAsync("Active cell: B8");
        await Expect(sample.Cell(7, 1)).ToHaveTextAsync("3");
    }

    [Theory]
    [MemberData(nameof(Modes))]
    public async Task CopyAndPasteUseTheUnformattedValue(string mode)
    {
        var sample = await OpenAsync("form", mode);

        await sample.Cell(3, 3).ClickAsync();
        Assert.Equal("256000", await sample.DispatchClipboardAsync("copy", null));

        await sample.Cell(6, 2).ClickAsync();
        await sample.DispatchClipboardAsync("paste", "256000\r\n");
        await Expect(sample.Cell(6, 2)).ToHaveTextAsync("256,000");
        await Expect(sample.Log.First).ToHaveTextAsync("C7 Paste: (empty) → 256000");

        await sample.Cell(3, 1).ClickAsync();
        Assert.Equal("2", await sample.DispatchClipboardAsync("cut", null));
        await Expect(sample.Cell(3, 1)).ToHaveTextAsync(string.Empty);
    }

    [Fact]
    public async Task ClipboardShortcutsReachTheSheetInTheReadyState()
    {
        var sample = await OpenAsync("form", "server");

        // Capture-phase listeners stop each clipboard event before the sheet handles it,
        // so the OS clipboard is untouched (paste is cancelled, copy writes nothing)
        await sample.Page.EvaluateAsync(
            """
            () => {
                window.clipboardEvents = [];
                for (const type of ['copy', 'cut', 'paste']) {
                    window.addEventListener(type, e => {
                        window.clipboardEvents.push(`${type}:${e.target.className}:${e.target.readOnly}`);
                        e.stopImmediatePropagation();
                        if (type === 'paste') {
                            e.preventDefault();
                        }
                    }, true);
                }
            }
            """);

        await sample.Cell(3, 3).ClickAsync();
        await sample.Keyboard.PressAsync("Control+c");
        await sample.Keyboard.PressAsync("Control+x");
        await sample.Keyboard.PressAsync("Control+v");
        await sample.Cell(6, 2).ClickAsync();
        await sample.Keyboard.PressAsync("Control+c");
        await sample.Keyboard.PressAsync("Control+v");

        string[] expected = ["copy:mussel-sheet-sink:true", "cut:mussel-sheet-sink:true", "paste:mussel-sheet-sink:true", "copy:mussel-sheet-sink:false", "paste:mussel-sheet-sink:false"];
        Assert.Equal(expected, await sample.Page.EvaluateAsync<string[]>("() => window.clipboardEvents"));
    }

    [Theory]
    [MemberData(nameof(Modes))]
    public async Task SelectedRangeIsClearedAndRestoredAtOnce(string mode)
    {
        var sample = await OpenAsync("form", mode);

        await sample.Cell(3, 1).ClickAsync();
        await sample.Keyboard.PressAsync("Shift+ArrowDown");
        await sample.Keyboard.PressAsync("Shift+ArrowDown");
        await sample.Keyboard.PressAsync("Shift+ArrowRight");
        await Expect(sample.Selection).ToHaveTextAsync("Selection: B4:C6");

        await sample.Keyboard.PressAsync("Delete");
        await Expect(sample.Cell(5, 2)).ToHaveTextAsync(string.Empty);
        await Expect(sample.Cell(8, 3)).ToHaveTextAsync("0");

        await sample.Keyboard.PressAsync("Control+z");
        await Expect(sample.Cell(5, 2)).ToHaveTextAsync("32,000");
        await Expect(sample.Cell(8, 3)).ToHaveTextAsync("332,000");
        await Expect(sample.Selection).ToHaveTextAsync("Selection: B4:C6");
    }

    [Theory]
    [MemberData(nameof(Modes))]
    public async Task PastedTableIsSelectedAndEnterMovesInsideIt(string mode)
    {
        var sample = await OpenAsync("form", mode);

        await sample.Cell(6, 1).ClickAsync();
        await sample.DispatchClipboardAsync("paste", "3\t100\r\n4\t200\r\n");
        await Expect(sample.Cell(7, 2)).ToHaveTextAsync("200");
        await Expect(sample.Cell(7, 3)).ToHaveTextAsync("800");
        await Expect(sample.Selection).ToHaveTextAsync("Selection: B7:C8");

        await sample.Keyboard.PressAsync("Enter");
        await Expect(sample.ActiveCell).ToHaveTextAsync("Active cell: B8");
        await sample.Keyboard.PressAsync("Enter");
        await Expect(sample.ActiveCell).ToHaveTextAsync("Active cell: C7");
        await Expect(sample.Selection).ToHaveTextAsync("Selection: B7:C8");

        Assert.Equal("3\t100\r\n4\t200\r\n", await sample.DispatchClipboardAsync("copy", null));
    }

    [Fact]
    public async Task HeadersDragAndCtrlASelectRanges()
    {
        var sample = await OpenAsync("form", "server");

        await sample.ColumnHeader(2).ClickAsync();
        await Expect(sample.Selection).ToHaveTextAsync("Selection: C1:C9");
        await sample.RowHeader(4).ClickAsync();
        await Expect(sample.Selection).ToHaveTextAsync("Selection: A5:D5");

        await sample.Cell(3, 0).DragToAsync(sample.Cell(5, 2));
        await Expect(sample.Selection).ToHaveTextAsync("Selection: A4:C6");
        await Expect(sample.ActiveCell).ToHaveTextAsync("Active cell: A4");

        await sample.Keyboard.PressAsync("Control+a");
        await Expect(sample.Selection).ToHaveTextAsync("Selection: A1:D9");
    }

    [Fact]
    public async Task LargePasteIsStreamedToTheServer()
    {
        var sample = await OpenAsync("large-sheets", "server");

        // 1,000 rows x 49 columns (about 330KB) exceeds Interactive Server's 32KB message limit,
        // so the paste has to be streamed
        await sample.Cell(0, 1).ClickAsync();
        var text = string.Concat(Enumerable.Range(0, 1000).Select(r => string.Join('\t', Enumerable.Range(0, 49).Select(c => (r * 1000) + c)) + "\r\n"));
        await sample.DispatchClipboardAsync("paste", text);

        await Expect(sample.Log.First).ToHaveTextAsync("AX1000 Paste: 49000 → 999048");
        await Expect(sample.Cell(1, 2)).ToHaveTextAsync("1,001");
        await Expect(sample.Selection).ToHaveTextAsync("Selection: B1:AX1000");
    }

    [Theory]
    [MemberData(nameof(Modes))]
    public async Task CtrlEnterAndCtrlDFillTheSelection(string mode)
    {
        var sample = await OpenAsync("form", mode);

        await sample.Cell(6, 1).ClickAsync();
        await sample.Keyboard.PressAsync("Shift+ArrowDown");
        await sample.Keyboard.PressAsync("Shift+ArrowRight");
        await sample.Keyboard.TypeAsync("5");
        await sample.Keyboard.PressAsync("Control+Enter");
        await Expect(sample.Cell(7, 2)).ToHaveTextAsync("5");
        await Expect(sample.Cell(7, 3)).ToHaveTextAsync("25");
        await Expect(sample.ActiveCell).ToHaveTextAsync("Active cell: B7");
        await Expect(sample.Selection).ToHaveTextAsync("Selection: B7:C8");

        await sample.Cell(3, 1).ClickAsync();
        await sample.Keyboard.PressAsync("Shift+ArrowDown");
        await sample.Keyboard.PressAsync("Control+d");
        await Expect(sample.Cell(4, 1)).ToHaveTextAsync("2");
        await Expect(sample.Log.First).ToHaveTextAsync("B5 Fill: 5 → 2");
    }

    [Fact]
    public async Task CtrlArrowsJumpToTheEdgesOfTheData()
    {
        var sample = await OpenAsync("input-columns", "server");

        await sample.Cell(0, 1).ClickAsync();
        await sample.Keyboard.PressAsync("Control+ArrowDown");
        await Expect(sample.ActiveCell).ToHaveTextAsync("Active cell: B200");
        await sample.Keyboard.PressAsync("Control+Shift+ArrowUp");
        await Expect(sample.Selection).ToHaveTextAsync("Selection: B1:B200");
        await sample.Keyboard.PressAsync("Control+End");
        await Expect(sample.ActiveCell).ToHaveTextAsync("Active cell: D200");
        await sample.Keyboard.PressAsync("Control+Home");
        await Expect(sample.ActiveCell).ToHaveTextAsync("Active cell: A1");
    }

    [Fact]
    public async Task AltEnterBreaksLinesAndCtrlSemicolonEntersToday()
    {
        var sample = await OpenAsync("cell-kinds", "server");

        await sample.Cell(17, 1).ClickAsync();
        await sample.Keyboard.PressAsync("F2");
        await sample.Keyboard.PressAsync("Alt+Enter");
        await sample.Keyboard.TypeAsync("Third line");
        await sample.Keyboard.PressAsync("Enter");
        await Expect(sample.Cell(17, 1)).ToHaveTextAsync("First line\nThe second line is long and wraps to the width of the cell\nThird line");

        await sample.Cell(0, 1).ClickAsync();
        await sample.Keyboard.PressAsync("Control+;");
        await sample.Keyboard.PressAsync("Enter");
        await Expect(sample.Cell(0, 1)).ToHaveTextAsync(DateTime.Now.ToString("M/d/yyyy", CultureInfo.InvariantCulture));
    }

    [Theory]
    [MemberData(nameof(Modes))]
    public async Task RowMenuInsertsAndDeletesDetailRowsThatCanBeUndone(string mode)
    {
        var sample = await OpenAsync("form", mode);

        await SamplePage.RightClickAsync(sample.RowHeader(4));
        await Expect(sample.Menu).ToBeVisibleAsync();
        await sample.MenuItem("insertRowsBelow").ClickAsync();
        await Expect(sample.Menu).ToBeHiddenAsync();
        await Expect(sample.Grid).ToHaveAttributeAsync("aria-rowcount", "11");
        await Expect(sample.Log.First).ToHaveTextAsync("Insert 1 row(s) at row 6");
        await Expect(sample.Selection).ToHaveTextAsync("Selection: A6:D6");
        await Expect(sample.Cell(9, 2)).ToHaveTextAsync("Total");

        await SamplePage.RightClickAsync(sample.RowHeader(4));
        await sample.MenuItem("deleteRows").ClickAsync();
        await Expect(sample.Grid).ToHaveAttributeAsync("aria-rowcount", "10");
        await Expect(sample.Log.First).ToHaveTextAsync("Delete 1 row(s) at row 5");
        await Expect(sample.Cell(8, 3)).ToHaveTextAsync("320,000");

        await sample.Keyboard.PressAsync("Control+z");
        await Expect(sample.Cell(4, 0)).ToHaveTextAsync("Mouse");
        await Expect(sample.Cell(9, 3)).ToHaveTextAsync("332,000");
        await Expect(sample.Selection).ToHaveTextAsync("Selection: A5:D5");
        await sample.Keyboard.PressAsync("Control+z");
        await Expect(sample.Grid).ToHaveAttributeAsync("aria-rowcount", "10");
        await Expect(sample.Cell(5, 0)).ToHaveTextAsync("Monitor");
    }

    [Fact]
    public async Task ShiftF10OpensTheMenuAndKeysChooseAnItem()
    {
        var sample = await OpenAsync("form", "server");

        await sample.Cell(3, 1).ClickAsync();
        await sample.Keyboard.PressAsync("Shift+F10");
        await Expect(sample.Menu).ToBeVisibleAsync();
        await Expect(sample.Page.Locator(".mussel-sheet-menu-item.mussel-sheet-focus")).ToHaveTextAsync("CutCtrl+X");
        await sample.Keyboard.PressAsync("Escape");
        await Expect(sample.Menu).ToBeHiddenAsync();

        await sample.Keyboard.PressAsync("Shift+F10");
        await sample.Keyboard.PressAsync("ArrowDown");
        await sample.Keyboard.PressAsync("ArrowDown");
        await sample.Keyboard.PressAsync("ArrowDown");
        await Expect(sample.Page.Locator(".mussel-sheet-menu-item.mussel-sheet-focus")).ToHaveTextAsync("Insert 1 row(s) above");
        await sample.Keyboard.PressAsync("Enter");
        await Expect(sample.Log.First).ToHaveTextAsync("Insert 1 row(s) at row 4");
        await Expect(sample.ActiveCell).ToHaveTextAsync("Active cell: B4");
        await Expect(sample.Cell(3, 1)).ToHaveAttributeAsync("data-kind", "number");
        await Expect(sample.Cell(4, 1)).ToHaveTextAsync("2");
    }

    [Fact]
    public async Task MenuCopyCutAndPasteUseTheAsyncClipboard()
    {
        var sample = await OpenAsync("form", "server");
        await sample.FakeAsyncClipboardAsync();

        await SamplePage.RightClickAsync(sample.Cell(3, 3));
        await Expect(sample.MenuItem("cut")).ToHaveAttributeAsync("aria-disabled", "true");
        await sample.MenuItem("copy").ClickAsync();
        await sample.WaitForClipboardAsync("256000");

        await SamplePage.RightClickAsync(sample.Cell(6, 2));
        await sample.MenuItem("paste").ClickAsync();
        await Expect(sample.Cell(6, 2)).ToHaveTextAsync("256,000");
        await Expect(sample.Log.First).ToHaveTextAsync("C7 Paste: (empty) → 256000");
        await sample.Page.EvaluateAsync("() => { window.fakeClipboard.text = ''; }");
        await SamplePage.RightClickAsync(sample.Cell(6, 2));
        await sample.MenuItem("cut").ClickAsync();
        await Expect(sample.Cell(6, 2)).ToHaveTextAsync(string.Empty);
        await sample.WaitForClipboardAsync("256000");

        await sample.Page.EvaluateAsync("() => { window.fakeClipboard.deny = true; }");
        await SamplePage.RightClickAsync(sample.Cell(6, 2));
        await sample.MenuItem("paste").ClickAsync();
        await Expect(sample.Page.Locator(".mussel-sheet-tip")).ToHaveTextAsync("Could not paste from the menu. Press Ctrl+V to paste.");
    }

    [Fact]
    public async Task ColumnMenuInsertsAColumnThatCanBeUndone()
    {
        var sample = await OpenAsync("large-sheets", "server");
        await Expect(sample.Grid).ToHaveAttributeAsync("aria-colcount", "51");

        await SamplePage.RightClickAsync(sample.ColumnHeader(2));
        await sample.MenuItem("insertColumnsRight").ClickAsync();
        await Expect(sample.Grid).ToHaveAttributeAsync("aria-colcount", "52");
        await Expect(sample.Cell(0, 3)).ToHaveTextAsync(string.Empty);
        await Expect(sample.Cell(0, 3)).ToHaveAttributeAsync("data-kind", "number");
        await Expect(sample.Cell(0, 4)).ToHaveTextAsync("3");

        // A Ctrl+Z pressed while the insert result is still pending is queued, not dropped
        await sample.Keyboard.PressAsync("Control+z");
        await Expect(sample.Grid).ToHaveAttributeAsync("aria-colcount", "51");
        await Expect(sample.Cell(0, 3)).ToHaveTextAsync("3");
        await Expect(sample.Selection).ToHaveTextAsync("Selection: C1:C1000");
    }

    [Theory]
    [MemberData(nameof(Modes))]
    public async Task FrozenRowAndColumnStayInPlaceWhileScrolling(string mode)
    {
        var sample = await OpenAsync("large-sheets", mode);

        var a1 = await SamplePage.RectAsync(sample.Cell(0, 0));
        await sample.ScrollSheetAsync(400, 300);
        await Expect(sample.Cell(30, 0)).ToBeAttachedAsync();
        Assert.Equal(a1, await SamplePage.RectAsync(sample.Cell(0, 0)));
        Assert.Equal(a1[0], (await SamplePage.RectAsync(sample.Cell(30, 0)))[0]);
        Assert.Equal(a1[1], (await SamplePage.RectAsync(sample.Cell(0, 9)))[1]);

        await sample.ScrollSheetAsync(0, 300);
        await sample.Cell(0, 1).ClickAsync();
        await Expect(sample.ActiveCell).ToHaveTextAsync("Active cell: B1");
        Assert.Equal(300, await sample.SheetScrollTopAsync());
        var b1 = await SamplePage.RectAsync(sample.Cell(0, 1));
        var frame = await SamplePage.RectAsync(sample.Page.Locator(".mussel-sheet-active"));
        Assert.Equal(b1[1] - 1, frame[1]);

        await sample.Keyboard.TypeAsync("5");
        await sample.Keyboard.PressAsync("Enter");
        await Expect(sample.Cell(0, 1)).ToHaveTextAsync("5");
        await Expect(sample.ActiveCell).ToHaveTextAsync("Active cell: B2");
        Assert.Equal(b1[1] + b1[3], (await SamplePage.RectAsync(sample.Cell(1, 1)))[1]);
        Assert.Equal(b1[3], (await SamplePage.RectAsync(sample.Page.Locator(".mussel-sheet-row[data-r='0']")))[3]);

        await sample.Cell(4, 0).ClickAsync(new() { Modifiers = [KeyboardModifier.Shift] });
        await Expect(sample.Selection).ToHaveTextAsync("Selection: A2:B5");
        await Expect(sample.Page.Locator(".mussel-sheet-pane-l > .mussel-sheet-selection")).ToBeVisibleAsync();

        await sample.ScrollSheetAsync(300, 500);
        await sample.Keyboard.PressAsync("Control+Home");
        await Expect(sample.ActiveCell).ToHaveTextAsync("Active cell: B2");
        Assert.Equal(0, await sample.SheetScrollTopAsync());
    }

    [Theory]
    [MemberData(nameof(Modes))]
    public async Task LargeSheetVirtualizesRowsAndColumns(string mode)
    {
        var sample = await OpenAsync("large-sheets?rows=10000", mode);

        Assert.True(await sample.Page.Locator(".mussel-sheet-row[data-r='0'] .mussel-sheet-cell").CountAsync() < 50);

        // Row 9001 starts at 24px x 9000 plus 16px for each of the 180 rows that are 40px tall (every 50th);
        // column AT starts at 120 + 100 x 44, and the scroll offsets below stop a little short of both
        var a1 = await SamplePage.RectAsync(sample.Cell(0, 0));
        var header = await SamplePage.RectAsync(sample.ColumnHeader(1));
        await sample.ScrollSheetAsync(4160, 218680);
        await Expect(sample.Cell(9000, 45)).ToBeAttachedAsync();
        Assert.Equal(a1, await SamplePage.RectAsync(sample.Cell(0, 0)));
        Assert.Equal(header[1], (await SamplePage.RectAsync(sample.ColumnHeader(45)))[1]);

        await sample.Cell(9000, 45).ClickAsync();
        await Expect(sample.ActiveCell).ToHaveTextAsync("Active cell: AT9001");
        await sample.Keyboard.TypeAsync("12345");
        var cell = await SamplePage.RectAsync(sample.Cell(9000, 45));
        var frame = await SamplePage.RectAsync(sample.Page.Locator(".mussel-sheet-active"));
        var sink = await SamplePage.RectAsync(sample.Page.Locator(".mussel-sheet-sink"));
        int[] corner = [cell[0] - 1, cell[1] - 1];
        Assert.Equal(corner, frame[..2]);
        Assert.Equal(cell[..2], sink[..2]);
        await sample.Keyboard.PressAsync("Enter");
        await Expect(sample.Cell(9000, 45)).ToHaveTextAsync("12,345");
        await Expect(sample.ActiveCell).ToHaveTextAsync("Active cell: AT9002");
        await sample.ScrollSheetAsync(4200, 218800);
        cell = await SamplePage.RectAsync(sample.Cell(9001, 45));
        frame = await SamplePage.RectAsync(sample.Page.Locator(".mussel-sheet-active"));
        corner = [cell[0] - 1, cell[1] - 1];
        Assert.Equal(corner, frame[..2]);

        await sample.FakeAsyncClipboardAsync();
        await sample.Keyboard.PressAsync("Shift+Home");
        await Expect(sample.Selection).ToHaveTextAsync("Selection: A9002:AT9002");
        await sample.DispatchClipboardAsync("copy", null);
        var expected = string.Join('\t', ["Row 9002", .. Enumerable.Range(1, 45).Select(c => (9002 * c).ToString(CultureInfo.InvariantCulture))]) + "\r\n";
        await sample.WaitForClipboardAsync(expected);
    }

    [Theory]
    [MemberData(nameof(Modes))]
    public async Task MergedCellsActAsOneCell(string mode)
    {
        var sample = await OpenAsync("merged-cells", mode);

        await sample.Cell(1, 1).ClickAsync();
        await Expect(sample.ActiveCell).ToHaveTextAsync("Active cell: B2");
        var anchor = await SamplePage.RectAsync(sample.Cell(1, 1));
        var frame = await SamplePage.RectAsync(sample.Page.Locator(".mussel-sheet-active"));
        Assert.Equal(anchor[2] + 1, frame[2]);

        await sample.Keyboard.PressAsync("Enter");
        await Expect(sample.ActiveCell).ToHaveTextAsync("Active cell: B3");
        await sample.Keyboard.PressAsync("Enter");
        await Expect(sample.ActiveCell).ToHaveTextAsync("Active cell: B5");

        await sample.Cell(6, 2).ClickAsync();
        await sample.Keyboard.PressAsync("ArrowDown");
        await Expect(sample.ActiveCell).ToHaveTextAsync("Active cell: A8");
        await sample.Keyboard.PressAsync("ArrowDown");
        await Expect(sample.ActiveCell).ToHaveTextAsync("Active cell: A9");
        await sample.Keyboard.PressAsync("ArrowRight");
        await Expect(sample.ActiveCell).ToHaveTextAsync("Active cell: B9");

        await sample.Keyboard.PressAsync("Shift+ArrowUp");
        await Expect(sample.Selection).ToHaveTextAsync("Selection: A8:D11");

        await sample.Cell(0, 0).ClickAsync(new() { Position = new() { X = 400, Y = 10 } });
        await Expect(sample.ActiveCell).ToHaveTextAsync("Active cell: A1");

        await sample.Cell(8, 1).ClickAsync();
        await sample.Keyboard.PressAsync("F2");
        await sample.Keyboard.PressAsync("Alt+Enter");
        await sample.Keyboard.TypeAsync("Added");
        Assert.Equal((await SamplePage.RectAsync(sample.Cell(8, 1)))[3] - 1, (await SamplePage.RectAsync(sample.Page.Locator(".mussel-sheet-sink")))[3]);
        await sample.Keyboard.PressAsync("Enter");
        await Expect(sample.Cell(8, 1)).ToHaveTextAsync("Delivery: one week after the order\nValid for 30 days\nAdded");
    }

    [Theory]
    [MemberData(nameof(Modes))]
    public async Task InputAssistFeaturesWork(string mode)
    {
        var sample = await OpenAsync("input-assist", mode);

        await sample.Cell(1, 1).ClickAsync();
        await Expect(sample.Note).ToBeVisibleAsync();
        await Expect(sample.Note).ToContainTextAsync("The year can be omitted");
        var cell = await SamplePage.RectAsync(sample.Cell(1, 1));
        Assert.True((await SamplePage.RectAsync(sample.Note))[1] >= cell[1] + cell[3]);

        await sample.Keyboard.TypeAsync("1105");
        await sample.Keyboard.PressAsync("Enter");
        await Expect(sample.Cell(1, 1)).ToHaveTextAsync($"11/5/{DateTime.Now.Year}");
        await Expect(sample.ActiveCell).ToHaveTextAsync("Active cell: D2");
        await Expect(sample.Note).ToContainTextAsync("Alt+Down opens the list");
        await sample.Keyboard.PressAsync("Tab");
        await Expect(sample.ActiveCell).ToHaveTextAsync("Active cell: B3");
        await sample.Keyboard.PressAsync("Tab");
        await sample.Keyboard.PressAsync("Tab");
        await Expect(sample.ActiveCell).ToHaveTextAsync("Active cell: B2");

        await sample.Cell(4, 3).ClickAsync();
        await sample.Keyboard.PressAsync("Tab");
        await Expect(sample.ActiveCell).ToHaveTextAsync("Active cell: A6");

        await Expect(sample.Changed).ToHaveTextAsync("Changed cells: B2");
        await Expect(sample.Page.Locator(".mussel-sheet-changed")).ToHaveCountAsync(1);
        await sample.Page.GetByRole(AriaRole.Button, new() { Name = "Mark as saved" }).ClickAsync();
        await Expect(sample.Changed).ToHaveTextAsync("Changed cells: none");
        await Expect(sample.Page.Locator(".mussel-sheet-changed")).ToHaveCountAsync(0);
        await Expect(sample.Note).ToBeHiddenAsync();
    }

    [Fact]
    public async Task ActiveFrameIsDrawnOverTheHeaderLines()
    {
        var sample = await OpenAsync("form", "server");

        await sample.Cell(0, 1).ClickAsync();
        var frame = await SamplePage.RectAsync(sample.Page.Locator(".mussel-sheet-active"));
        Assert.Equal([frame[0], frame[1], frame[2], 1], await SamplePage.RectAsync(sample.Page.Locator(".mussel-sheet-pane-ts > .mussel-sheet-cap")));

        await sample.Cell(3, 0).ClickAsync();
        frame = await SamplePage.RectAsync(sample.Page.Locator(".mussel-sheet-active"));
        Assert.Equal([frame[0], frame[1], 1, frame[3]], await SamplePage.RectAsync(sample.Page.Locator(".mussel-sheet-pane-l > .mussel-sheet-cap")));

        await sample.Cell(0, 1).ClickAsync();
        await sample.Keyboard.TypeAsync("abc");
        await sample.Keyboard.PressAsync("Enter");
        await Expect(sample.Page.Locator(".mussel-sheet-sink.mussel-sheet-invalid")).ToBeVisibleAsync();
        frame = await SamplePage.RectAsync(sample.Page.Locator(".mussel-sheet-active"));
        Assert.Equal([frame[0] + 1, frame[1] + 1, frame[2] - 2, frame[3] - 2], await SamplePage.RectAsync(sample.Page.Locator(".mussel-sheet-sink")));
        await Expect(sample.Page.Locator(".mussel-sheet-pane-ts > .mussel-sheet-cap")).ToHaveClassAsync("mussel-sheet-cap mussel-sheet-invalid");
    }

    [Fact]
    public async Task RegisteredKeysAndMenuItemsWork()
    {
        var sample = await OpenAsync("customization", "server");

        await sample.Cell(3, 1).ClickAsync();
        await sample.Keyboard.PressAsync("F4");
        await Expect(sample.Page.Locator(".mussel-sheet-sink.mussel-sheet-editing")).ToBeVisibleAsync();
        await sample.Keyboard.PressAsync("Escape");

        await SamplePage.RightClickAsync(sample.Cell(4, 2));
        var item = sample.Page.Locator(".mussel-sheet-menu-item[data-name='log-selection']");
        await Expect(item).ToHaveTextAsync("Log the selection");
        await item.ClickAsync();
        await Expect(sample.Log.First).ToHaveTextAsync("Menu item log-selection: C5");
    }

    [Theory]
    [MemberData(nameof(Modes))]
    public async Task JapaneseFormReadsJapaneseInput(string mode)
    {
        var sample = await OpenAsync("japanese-form", mode);

        await Expect(sample.Cell(1, 1)).ToHaveTextAsync("令和8年11月5日");
        await sample.Cell(1, 1).ClickAsync();
        await sample.Keyboard.TypeAsync("R7.11.5");
        await sample.Keyboard.PressAsync("Enter");
        await Expect(sample.Cell(1, 1)).ToHaveTextAsync("令和7年11月5日");

        await sample.Cell(2, 1).ClickAsync();
        await sample.Keyboard.TypeAsync("12月25日");
        await sample.Keyboard.PressAsync("Enter");
        await Expect(sample.Cell(2, 1)).ToHaveTextAsync($"{DateTime.Now.Year}/12/25");

        await sample.Cell(3, 1).ClickAsync();
        await sample.Keyboard.TypeAsync("1000002");
        await sample.Keyboard.PressAsync("Enter");
        await Expect(sample.Cell(3, 1)).ToHaveTextAsync("100-0002");

        await sample.Cell(4, 1).ClickAsync();
        await sample.Keyboard.TypeAsync("ひらがな");
        await sample.Keyboard.PressAsync("Enter");
        await Expect(sample.Cell(4, 1)).ToHaveTextAsync("ヒラガナ");

        await Expect(sample.Cell(13, 5)).ToHaveTextAsync("10,500");
        await Expect(sample.Cell(14, 5)).ToHaveTextAsync("1,050");
        await Expect(sample.Cell(15, 5)).ToHaveTextAsync("11,550");
        await sample.Cell(8, 3).ClickAsync();
        await sample.Keyboard.TypeAsync("20");
        await sample.Keyboard.PressAsync("Enter");
        await Expect(sample.Cell(15, 5)).ToHaveTextAsync("16,500");
    }

    [Theory]
    [MemberData(nameof(Modes))]
    public async Task JapaneseFormCreatesAPdf(string mode)
    {
        var sample = await OpenAsync("japanese-form", mode);

        await sample.Cell(8, 3).ClickAsync();
        await sample.Keyboard.TypeAsync("20");

        // Focus moves to the button, so the typed value is committed before the PDF is made
        var sent = sample.Page.WaitForRequestAsync("**/api/reports/invoice");
        var download = await sample.Page.RunAndWaitForDownloadAsync(() => sample.Page.GetByRole(AriaRole.Button, new() { Name = "PDF を作成" }).ClickAsync());
        var json = (await sent.ConfigureAwait(true)).PostData;

        Assert.Equal("請求書.pdf", download.SuggestedFilename);
        Assert.Contains("\"Quantity\":\"20\"", json, StringComparison.Ordinal);
        Assert.Contains("\"Total\":\"16,500\"", json, StringComparison.Ordinal);
        Assert.Contains("\"InvoiceDate\":\"\\u4EE4\\u548C8\\u5E7411\\u67085\\u65E5\"", json, StringComparison.OrdinalIgnoreCase);

        var pdf = await File.ReadAllBytesAsync(await download.PathAsync(), TestContext.Current.CancellationToken);
        Assert.Equal("%PDF"u8.ToArray(), pdf[..4]);
        await Expect(sample.Log.First).ToHaveTextAsync("PDF を作成しました");
    }

    [Theory]
    [MemberData(nameof(Modes))]
    public async Task LocalizationSwitchesTheLanguage(string mode)
    {
        var sample = await OpenAsync("localization", mode);
        var culture = sample.Page.Locator(".sample-culture");

        await Expect(culture).ToContainTextAsync("Culture: en-US / UI culture: en-US / Sheet.Culture: en-US");
        await Expect(sample.Cell(2, 1)).ToHaveTextAsync("11/5/2026");
        await SamplePage.RightClickAsync(sample.Cell(1, 1));
        await Expect(sample.MenuItem("copy")).ToContainTextAsync("Copy");
        await sample.Keyboard.PressAsync("Escape");

        await sample.Page.GetByRole(AriaRole.Link, new() { Name = "日本語" }).ClickAsync();
        await Expect(culture).ToContainTextAsync("Culture: ja-JP / UI culture: ja-JP / Sheet.Culture: ja-JP");
        await sample.WaitForSheetAsync();
        await Expect(sample.Page.Locator(".mode-state")).ToHaveTextAsync("Interactive");
        await Expect(sample.Cell(2, 1)).ToHaveTextAsync("2026/11/05");
        await SamplePage.RightClickAsync(sample.Cell(1, 1));
        await Expect(sample.MenuItem("copy")).ToContainTextAsync("コピー");
        await sample.Keyboard.PressAsync("Escape");

        await sample.Cell(1, 1).ClickAsync();
        await sample.Keyboard.TypeAsync("abc");
        await sample.Keyboard.PressAsync("Enter");
        await Expect(sample.Page.Locator(".mussel-sheet-tip")).ToHaveTextAsync("数値を入力してください");
        await sample.Keyboard.PressAsync("Escape");

        await sample.Page.GetByRole(AriaRole.Link, new() { Name = "English" }).ClickAsync();
        await Expect(culture).ToContainTextAsync("Culture: en-US");
        await Expect(sample.Cell(2, 1)).ToHaveTextAsync("11/5/2026");
    }

    [Fact]
    public async Task SheetDesignRoundTrips()
    {
        var sample = await OpenAsync("save-and-load", "server");
        var model = sample.Page.Locator(".sample-model");
        var rebuild = sample.Page.GetByRole(AriaRole.Button, new() { Name = "Load from the JSON" });

        await sample.Page.GetByRole(AriaRole.Button, new() { Name = "Save the design as JSON" }).ClickAsync();
        await Expect(model).ToContainTextAsync("\"kind\": \"number\"");
        await Expect(model).ToContainTextAsync("\"validator\": \"QuantityLimit\"");
        await Expect(model).ToContainTextAsync("\"function\": \"Amount\"");
        await Expect(model).ToContainTextAsync("\"frozenRows\": 3");

        await rebuild.ClickAsync();
        await Expect(sample.Page.Locator(".mussel-sheet-top .mussel-sheet-row")).ToHaveCountAsync(3);
        await Expect(sample.Cell(3, 0)).ToHaveTextAsync(string.Empty);
        await Expect(sample.Cell(2, 0)).ToHaveTextAsync("Item");
        await Expect(sample.Cell(6, 3)).ToHaveTextAsync("$0");
        await Expect(sample.Cell(6, 3)).ToHaveAttributeAsync("data-ro", "1");
        await sample.Cell(3, 1).ClickAsync();
        await sample.Keyboard.TypeAsync("3");
        await sample.Keyboard.PressAsync("Tab");
        await sample.Keyboard.TypeAsync("150");
        await sample.Keyboard.PressAsync("Enter");
        await Expect(sample.Cell(3, 3)).ToHaveTextAsync("450");
        await Expect(sample.Cell(6, 3)).ToHaveTextAsync("$450");

        await sample.Page.GetByRole(AriaRole.Button, new() { Name = "Save with the values" }).ClickAsync();
        await Expect(model).ToContainTextAsync("\"type\": \"number\"");
        await rebuild.ClickAsync();
        await Expect(sample.Cell(3, 1)).ToHaveTextAsync("3");
        await Expect(sample.Cell(6, 3)).ToHaveTextAsync("$450");
    }

    [Fact]
    public async Task ThemeVariablesChangeTheColors()
    {
        var sample = await OpenAsync("form", "server");
        var cell = sample.Cell(3, 0);
        await Expect(cell).ToHaveCSSAsync("color", "rgb(31, 35, 40)");
        await Expect(cell).ToHaveCSSAsync("background-color", "rgb(255, 255, 255)");

        await sample.Page.AddStyleTagAsync(new() { Content = "body { --mussel-primary-text: rgb(1, 2, 3); } .mussel-sheet { --mussel-sheet-cell-bg: rgb(4, 5, 6); }" });
        await Expect(cell).ToHaveCSSAsync("color", "rgb(1, 2, 3)");
        await Expect(cell).ToHaveCSSAsync("background-color", "rgb(4, 5, 6)");

        // Inside .mussel-dark of MusselChart the shared variables must not reach the sheet: there is no dark theme yet
        await sample.Page.AddStyleTagAsync(new() { Content = ".mussel-dark { --mussel-primary-text: rgb(240, 239, 236); }" });
        await sample.Page.EvaluateAsync("document.body.classList.add('mussel-dark')");
        await Expect(cell).ToHaveCSSAsync("color", "rgb(31, 35, 40)");
    }

    [Fact]
    public async Task ComputedCellsWork()
    {
        var sample = await OpenAsync("computed-cells", "server");

        await Expect(sample.Cell(3, 3)).ToHaveTextAsync("1,200");
        await Expect(sample.Cell(6, 3)).ToHaveTextAsync("2,800");
        await Expect(sample.Cell(7, 3)).ToHaveTextAsync("3,080");
        await Expect(sample.Cell(1, 3)).ToHaveTextAsync("2");

        await sample.Cell(3, 1).ClickAsync();
        await sample.Keyboard.TypeAsync("100");
        await sample.Keyboard.PressAsync("Enter");
        await Expect(sample.Cell(3, 3)).ToHaveTextAsync("12,000");
        await Expect(sample.Cell(7, 3)).ToHaveTextAsync("14,960");

        await sample.Cell(0, 3).ClickAsync();
        await sample.Keyboard.TypeAsync("2");
        await sample.Keyboard.PressAsync("Enter");
        await Expect(sample.Cell(4, 3)).ToHaveTextAsync("3,200");
        await Expect(sample.Cell(7, 3)).ToHaveTextAsync("29,920");

        await sample.Cell(5, 0).ClickAsync();
        await sample.Keyboard.TypeAsync("Eraser");
        await sample.Keyboard.PressAsync("Enter");
        await Expect(sample.Cell(1, 3)).ToHaveTextAsync("3");
        await Expect(sample.Cell(3, 3)).ToHaveAttributeAsync("data-ro", "1");
    }

    [Fact]
    public async Task EnterOnReadOnlyCellDoesNotAddRows()
    {
        var sample = await OpenAsync("computed-cells", "server");

        await sample.Cell(3, 3).ClickAsync();
        await Expect(sample.ActiveCell).ToHaveTextAsync("Active cell: D4");
        await sample.Keyboard.PressAsync("Enter");
        await Expect(sample.ActiveCell).ToHaveTextAsync("Active cell: D4");

        await sample.Cell(5, 2).ClickAsync();
        await sample.Keyboard.PressAsync("Enter");
        await Expect(sample.ActiveCell).ToHaveTextAsync("Active cell: C7");
        await Expect(sample.Cell(7, 2)).ToHaveTextAsync("Subtotal");
        await Expect(sample.Log).ToHaveCountAsync(1);
        await Expect(sample.Log.First).ToHaveTextAsync("Insert 1 row(s) at row 7");
    }

    [Fact]
    public async Task CellCallbacksWork()
    {
        var sample = await OpenAsync("callbacks", "server");

        await Expect(sample.Cell(1, 1)).ToHaveTextAsync("$12,800");
        await Expect(sample.Cell(3, 1)).ToHaveTextAsync("Approved");
        await Expect(sample.Cell(4, 1)).ToHaveCSSAsync("color", "rgb(198, 40, 40)");

        // Amount is rounded and code upper-cased before the rule is checked (JS only checks the type)
        await sample.Cell(1, 1).ClickAsync();
        await sample.Keyboard.TypeAsync("1234.5");
        await sample.Keyboard.PressAsync("Enter");
        await Expect(sample.Cell(1, 1)).ToHaveTextAsync("$1,235");
        await sample.Cell(2, 1).ClickAsync();
        await sample.Keyboard.TypeAsync("ab-12");
        await sample.Keyboard.PressAsync("Enter");
        await Expect(sample.Cell(2, 1)).ToHaveTextAsync("AB-12");

        await sample.Cell(7, 1).ClickAsync();
        await sample.Keyboard.TypeAsync("3");
        await sample.Keyboard.PressAsync("Enter");
        await Expect(sample.Cell(7, 1)).ToHaveAttributeAsync("aria-invalid", "true");
        await Expect(sample.Log.First).ToHaveTextAsync("B8 Input: 4 → 3 (error: Enter an even number.)");
    }

    [Fact]
    public async Task FormatDecorationsWork()
    {
        var sample = await OpenAsync("formatting", "server");

        await Expect(sample.Cell(0, 0)).ToHaveCSSAsync("font-size", "20px");
        await Expect(sample.Cell(6, 0)).ToHaveCSSAsync("text-decoration-line", "line-through");
        await Expect(sample.Cell(8, 1)).ToHaveCSSAsync("font-style", "italic");
        await Expect(sample.Cell(4, 0)).ToHaveCSSAsync("padding-left", "17px");

        var sink = sample.Page.Locator(".mussel-sheet-sink");
        await sample.Cell(2, 1).ClickAsync();
        await sample.Keyboard.TypeAsync("Trip");
        await Expect(sink).ToHaveCSSAsync("font-size", "16px");
        await Expect(sink).ToHaveCSSAsync("font-weight", "600");
        await sample.Keyboard.PressAsync("Escape");
        await Expect(sink).ToHaveCSSAsync("font-size", "13px");
    }

    [Fact]
    public async Task FillHandleWorks()
    {
        var sample = await OpenAsync("fill-handle", "server");
        var handle = sample.Page.Locator(".mussel-sheet-fill");

        await sample.Cell(1, 1).ClickAsync();
        await handle.DblClickAsync();
        await Expect(sample.Cell(2, 1)).ToHaveTextAsync("11/4/2026");
        await Expect(sample.Cell(11, 1)).ToHaveTextAsync("11/13/2026");

        await sample.Cell(1, 2).ClickAsync();
        await handle.HoverAsync();
        await sample.Page.Mouse.DownAsync();
        await sample.Cell(3, 2).HoverAsync();
        await sample.Page.Mouse.UpAsync();
        await Expect(sample.Cell(2, 2)).ToHaveTextAsync("Round 2");
        await Expect(sample.Cell(3, 2)).ToHaveTextAsync("Round 3");

        await sample.Cell(1, 4).ClickAsync();
        await sample.Cell(2, 4).ClickAsync(new() { Modifiers = [KeyboardModifier.Shift] });
        await handle.HoverAsync();
        await sample.Page.Mouse.DownAsync();
        await sample.Cell(4, 4).HoverAsync();
        await sample.Page.Mouse.UpAsync();
        await Expect(sample.Cell(3, 4)).ToHaveTextAsync("30");
        await Expect(sample.Cell(4, 4)).ToHaveTextAsync("40");
        await sample.Keyboard.PressAsync("Control+z");
        await Expect(sample.Cell(4, 4)).ToHaveTextAsync(string.Empty);
    }

    [Fact]
    public async Task ObjectBindingWorks()
    {
        var sample = await OpenAsync("object-binding", "server");
        var model = sample.Page.Locator(".sample-model");

        await Expect(sample.Cell(3, 0)).ToHaveTextAsync("Notebook");
        await Expect(sample.Cell(6, 3)).ToHaveTextAsync("2,800");

        await sample.Cell(3, 1).ClickAsync();
        await sample.Keyboard.TypeAsync("5");
        await sample.Keyboard.PressAsync("Enter");
        await Expect(sample.Cell(3, 3)).ToHaveTextAsync("600");
        await Expect(sample.Cell(6, 3)).ToHaveTextAsync("2,200");
        await Expect(model).ToContainTextAsync("1. Notebook / Quantity 5 / Unit price 120 / Amount 600");

        await sample.Cell(5, 0).ClickAsync();
        await sample.Keyboard.TypeAsync("Eraser");
        await sample.Keyboard.PressAsync("Enter");
        await Expect(model).ToContainTextAsync("Lines: 3");
        await Expect(sample.Cell(1, 3)).ToHaveTextAsync("3");

        await sample.Page.GetByRole(AriaRole.Button, new() { Name = "Add a line" }).ClickAsync();
        await Expect(sample.Cell(6, 0)).ToHaveTextAsync("Added line 1");
        await Expect(sample.Cell(7, 2)).ToHaveTextAsync("Total");
        await Expect(sample.Cell(7, 3)).ToHaveTextAsync("2,300");
    }

    [Fact]
    public async Task CustomEditorCommitsTheValue()
    {
        var sample = await OpenAsync("customization", "server");

        await Expect(sample.Cell(1, 1)).ToHaveTextAsync("#1a73e8");
        await sample.Cell(1, 1).ClickAsync();
        await sample.Keyboard.PressAsync("F2");
        await sample.Page.Locator(".custom-color[title='#d93025']").ClickAsync();
        await Expect(sample.Cell(1, 1)).ToHaveTextAsync("#d93025");
        await Expect(sample.Log.First).ToHaveTextAsync("B2 Input: #1a73e8 → #d93025");

        await Expect(sample.Cell(2, 1)).ToHaveTextAsync("★★★");
        await sample.Cell(2, 1).DblClickAsync();
        await sample.Page.Locator(".custom-choices").GetByRole(AriaRole.Button, new() { Name = "5" }).ClickAsync();
        await Expect(sample.Cell(2, 1)).ToHaveTextAsync("★★★★★");
    }

    [Fact]
    public async Task RegisteredTextReplacesTheBuiltInMessage()
    {
        var sample = await OpenAsync("customization", "server");

        await sample.Cell(3, 1).ClickAsync();
        await sample.Keyboard.PressAsync("F2");
        await sample.Keyboard.PressAsync("Control+a");
        await sample.Keyboard.PressAsync("Delete");
        await sample.Keyboard.PressAsync("Enter");
        await Expect(sample.Page.Locator(".mussel-sheet-tip")).ToHaveTextAsync("This cell is required.");
    }

    [Fact]
    public async Task ThemePageChangesTheColors()
    {
        var sample = await OpenAsync("theme", "server");
        var cell = sample.Cell(0, 1);
        var label = sample.Cell(0, 0);
        await Expect(cell).ToHaveCSSAsync("color", "rgb(31, 35, 40)");
        await Expect(label).ToHaveCSSAsync("background-color", "rgb(238, 242, 247)");

        await sample.Page.GetByRole(AriaRole.Radio, new() { Name = "Shared variables on a container" }).CheckAsync();
        await Expect(cell).ToHaveCSSAsync("color", "rgb(27, 58, 87)");

        await sample.Page.GetByRole(AriaRole.Radio, new() { Name = "Sheet variables on the sheet" }).CheckAsync();
        await Expect(cell).ToHaveCSSAsync("color", "rgb(31, 35, 40)");
        await Expect(label).ToHaveCSSAsync("background-color", "rgb(207, 232, 214)");

        await sample.Page.GetByRole(AriaRole.Radio, new() { Name = "Dark" }).CheckAsync();
        await Expect(cell).ToHaveCSSAsync("color", "rgb(240, 239, 236)");
        await Expect(cell).ToHaveCSSAsync("background-color", "rgb(24, 24, 22)");
        await Expect(label).ToHaveCSSAsync("background-color", "rgb(44, 44, 42)");
    }

    [Fact]
    public async Task PlaygroundChangesTheSettings()
    {
        var sample = await OpenAsync("playground", "server");

        await Expect(sample.RowHeader(0)).ToBeVisibleAsync();
        await sample.Page.GetByLabel("ShowRowHeaders").UncheckAsync();
        await Expect(sample.RowHeader(0)).ToHaveCountAsync(0);

        var direction = sample.Page.Locator(".mud-input-control.mud-select").Filter(new() { HasText = "MoveAfterReturnDirection" });
        await direction.ClickAsync();
        await sample.Page.Locator(".mud-popover-open .mud-list-item").Filter(new() { HasText = "Right" }).ClickAsync();
        await Expect(direction.Locator("input").First).ToHaveValueAsync("Right");
        await sample.Cell(0, 1).ClickAsync();
        await sample.Keyboard.TypeAsync("7");
        await sample.Keyboard.PressAsync("Enter");
        await Expect(sample.ActiveCell).ToHaveTextAsync("Active cell: C1");

        var undo = sample.Page.GetByRole(AriaRole.Button, new() { Name = "Undo" });
        await Expect(undo).ToBeEnabledAsync();
        await undo.ClickAsync();
        await Expect(sample.Cell(0, 1)).ToHaveTextAsync("1");
    }

    [Fact]
    public async Task ViewMethodsDriveTheSheet()
    {
        var sample = await OpenAsync("playground", "server");
        var sink = sample.Page.Locator(".mussel-sheet-sink");

        await sample.Page.GetByRole(AriaRole.Button, new() { Name = "SetActiveCellAsync" }).ClickAsync();
        await Expect(sample.ActiveCell).ToHaveTextAsync("Active cell: C2");

        await sample.Page.GetByRole(AriaRole.Button, new() { Name = "FocusAsync" }).ClickAsync();
        await Expect(sink).ToBeFocusedAsync();

        await sample.Page.GetByRole(AriaRole.Button, new() { Name = "BeginEditAsync" }).ClickAsync();
        await Expect(sample.Page.Locator(".mussel-sheet-sink.mussel-sheet-editing")).ToBeVisibleAsync();
        await Expect(sink).ToHaveValueAsync("200");
        await sink.FillAsync("250");
        await sample.Page.GetByRole(AriaRole.Button, new() { Name = "CommitEditAsync" }).ClickAsync();
        await Expect(sample.Cell(1, 2)).ToHaveTextAsync("250");

        await Expect(sample.Log.First).ToHaveTextAsync("C2 Input: 200 → 250");

        await sample.Page.GetByRole(AriaRole.Button, new() { Name = "BeginEditAsync" }).ClickAsync();
        await sink.FillAsync("999");
        await sample.Page.GetByRole(AriaRole.Button, new() { Name = "CancelEditAsync" }).ClickAsync();
        await Expect(sample.Page.Locator(".mussel-sheet-sink.mussel-sheet-editing")).ToHaveCountAsync(0);
        await Expect(sink).ToBeFocusedAsync();
        await Expect(sample.Cell(1, 2)).ToHaveTextAsync("250");
        await Expect(sample.Log.First).ToHaveTextAsync("C2 Input: 200 → 250");
    }

    [Fact]
    public async Task ColumnHeadersStopBelowAFixedPageHeader()
    {
        const int AppBarHeight = 48;
        const int LastRow = 11;

        var sample = await OpenAsync("playground", "server");
        await sample.Page.SetViewportSizeAsync(1280, 240);

        await sample.Cell(0, 0).ClickAsync();
        for (var i = 0; i < LastRow; i++)
        {
            await sample.Keyboard.PressAsync("ArrowDown");
        }

        await Expect(sample.ActiveCell).ToHaveTextAsync("Active cell: A12");
        var header = await SamplePage.RectAsync(sample.ColumnHeader(0));
        Assert.Equal(AppBarHeight, header[1]);

        for (var i = 0; i < LastRow; i++)
        {
            await sample.Keyboard.PressAsync("ArrowUp");
        }

        await Expect(sample.ActiveCell).ToHaveTextAsync("Active cell: A1");
        var cell = await SamplePage.RectAsync(sample.Cell(0, 0));
        Assert.True(cell[1] >= AppBarHeight + header[3], $"The cell is at {cell[1]}.");
    }

    [Fact]
    public async Task FocusLeavingTheSheetCommitsTheEdit()
    {
        var sample = await OpenAsync("form", "server");

        await sample.Cell(3, 1).ClickAsync();
        await sample.Keyboard.TypeAsync("9");
        await sample.Page.Locator(".sample-source .mud-expand-panel-header").First.ClickAsync();

        await Expect(sample.Cell(3, 1)).ToHaveTextAsync("9");
        await Expect(sample.ActiveCell).ToHaveTextAsync("Active cell: B4");
    }

    [Theory]
    [MemberData(nameof(Modes))]
    public async Task ListOpensWithTheKeysAndTheMouse(string mode)
    {
        var sample = await OpenAsync("cell-kinds", mode);
        var cell = sample.Cell(7, 1);
        var popup = sample.Page.Locator(".mussel-sheet-popup.mussel-sheet-open");
        var items = popup.Locator(".mussel-sheet-item");
        var focused = popup.Locator(".mussel-sheet-item.mussel-sheet-focus");

        await cell.ClickAsync();
        await sample.Keyboard.PressAsync("Alt+ArrowDown");
        await Expect(items).ToHaveTextAsync(["General affairs", "Sales", "Development"]);
        await Expect(focused).ToHaveTextAsync("Sales");
        await sample.Keyboard.PressAsync("ArrowDown");
        await sample.Keyboard.PressAsync("Enter");
        await Expect(cell).ToHaveTextAsync("Development");
        await Expect(popup).ToHaveCountAsync(0);

        await cell.ClickAsync();
        await sample.Page.Locator(".mussel-sheet-drop").ClickAsync();
        await items.Nth(0).HoverAsync();
        await Expect(focused).ToHaveTextAsync("General affairs");
        await items.Nth(0).ClickAsync();
        await Expect(cell).ToHaveTextAsync("General affairs");

        await sample.Keyboard.PressAsync("Alt+ArrowDown");
        await Expect(popup).ToBeVisibleAsync();
        await sample.Keyboard.PressAsync("Escape");
        await Expect(popup).ToHaveCountAsync(0);
        await Expect(cell).ToHaveTextAsync("General affairs");
    }

    [Theory]
    [MemberData(nameof(Modes))]
    public async Task CalendarPicksADate(string mode)
    {
        var sample = await OpenAsync("cell-kinds", mode);
        var cell = sample.Cell(4, 1);
        var popup = sample.Page.Locator(".mussel-sheet-popup.mussel-sheet-open");
        var title = popup.Locator(".mussel-sheet-cal-title");

        await cell.ClickAsync();
        await sample.Keyboard.PressAsync("Alt+ArrowDown");
        await Expect(title).ToHaveTextAsync("November 2026");
        await sample.Keyboard.PressAsync("ArrowRight");
        await sample.Keyboard.PressAsync("ArrowDown");
        await sample.Keyboard.PressAsync("Enter");
        await Expect(cell).ToHaveTextAsync("11/10/2026");
        await Expect(popup).ToHaveCountAsync(0);

        await cell.ClickAsync();
        await sample.Keyboard.PressAsync("Alt+ArrowDown");
        await sample.Keyboard.PressAsync("PageDown");
        await Expect(title).ToHaveTextAsync("December 2026");
        await popup.Locator(".mussel-sheet-cal-prev").ClickAsync();
        await popup.Locator(".mussel-sheet-cal-prev").ClickAsync();
        await Expect(title).ToHaveTextAsync("October 2026");
        await popup.Locator(".mussel-sheet-cal-next").ClickAsync();
        await popup.Locator(".mussel-sheet-cal-day[data-date='2026-11-20']").ClickAsync();
        await Expect(cell).ToHaveTextAsync("11/20/2026");

        // Days before the minimum (1/1/2026) cannot be picked
        await sample.Keyboard.PressAsync("Alt+ArrowDown");
        for (var i = 0; i < 10; i++)
        {
            await popup.Locator(".mussel-sheet-cal-prev").ClickAsync();
        }

        var early = popup.Locator(".mussel-sheet-cal-day[data-date='2025-12-31']");
        await Expect(early).ToHaveClassAsync(new System.Text.RegularExpressions.Regex("mussel-sheet-disabled"));
        await early.ClickAsync();
        await Expect(popup).ToBeVisibleAsync();

        await popup.Locator(".mussel-sheet-cal-today").ClickAsync();
        await Expect(cell).ToHaveTextAsync(DateTime.Now.ToString("M/d/yyyy", CultureInfo.InvariantCulture));
    }

    [Theory]
    [MemberData(nameof(Modes))]
    public async Task AutoCompleteSuggestsWhileTyping(string mode)
    {
        var sample = await OpenAsync("cell-kinds", mode);
        var cell = sample.Cell(15, 1);
        var items = sample.Page.Locator(".mussel-sheet-popup.mussel-sheet-open .mussel-sheet-item");

        await cell.ClickAsync();
        await sample.Keyboard.TypeAsync("a");
        await Expect(items).Not.ToHaveCountAsync(0);
        await sample.Keyboard.TypeAsync("l");
        await Expect(items).ToHaveTextAsync(["Alder Logistics", "Fern Chemicals"]);
        await sample.Keyboard.PressAsync("ArrowDown");
        await sample.Keyboard.PressAsync("Enter");
        await Expect(cell).ToHaveTextAsync("Alder Logistics");
        await Expect(items).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task CompositionStaysInTheCellUntilItIsConfirmed()
    {
        var sample = await OpenAsync("cell-kinds", "server");
        var sink = sample.Page.Locator(".mussel-sheet-sink");
        var cdp = await sample.Page.Context.NewCDPSessionAsync(sample.Page);

        await sample.Cell(0, 1).ClickAsync();
        await cdp.SendAsync("Input.imeSetComposition", new Dictionary<string, object> { ["text"] = "にほん", ["selectionStart"] = 3, ["selectionEnd"] = 3 });
        await Expect(sink).ToHaveValueAsync("にほん");
        await Expect(sample.Page.Locator(".mussel-sheet-sink.mussel-sheet-editing")).ToBeVisibleAsync();
        await Expect(sample.ActiveCell).ToHaveTextAsync("Active cell: B1");

        await cdp.SendAsync("Input.insertText", new Dictionary<string, object> { ["text"] = "日本" });
        await Expect(sink).ToHaveValueAsync("日本");

        // An Enter right after the composition ends belongs to the IME
        await sample.Page.WaitForTimeoutAsync(200);
        await sample.Keyboard.PressAsync("Enter");
        await Expect(sample.Cell(0, 1)).ToHaveTextAsync("日本");
        await Expect(sample.ActiveCell).ToHaveTextAsync("Active cell: B2");
    }

    [Fact]
    public async Task PageKeysMoveByAScreen()
    {
        var sample = await OpenAsync("input-columns", "server");

        await sample.Cell(0, 1).ClickAsync();
        await sample.Keyboard.PressAsync("PageDown");
        await Expect(sample.ActiveCell).ToHaveTextAsync(new System.Text.RegularExpressions.Regex("^Active cell: B1[0-9]$"));
        await sample.Keyboard.PressAsync("PageUp");
        await Expect(sample.ActiveCell).ToHaveTextAsync("Active cell: B1");
    }

    [Fact]
    public async Task CustomEditorFollowsTheKeys()
    {
        var sample = await OpenAsync("customization", "server");
        var editor = sample.Page.Locator(".mussel-sheet-custom.mussel-sheet-open");

        await sample.Cell(1, 1).ClickAsync();
        await sample.Keyboard.PressAsync("Alt+ArrowDown");
        await Expect(editor).ToBeVisibleAsync();
        await sample.Keyboard.PressAsync("Escape");
        await Expect(editor).ToHaveCountAsync(0);
        await Expect(sample.Cell(1, 1)).ToHaveTextAsync("#1a73e8");
        await Expect(sample.ActiveCell).ToHaveTextAsync("Active cell: B2");

        await sample.Keyboard.PressAsync("F2");
        await Expect(editor).ToBeVisibleAsync();
        await sample.Keyboard.PressAsync("Tab");
        await Expect(editor).ToHaveCountAsync(0);
        await Expect(sample.ActiveCell).ToHaveTextAsync("Active cell: B3");
    }

    [Fact]
    public async Task AWrongPasteStaysInTheCellWithAMessage()
    {
        var sample = await OpenAsync("cell-kinds", "server");
        var tip = sample.Page.Locator(".mussel-sheet-tip");

        await sample.Cell(2, 1).ClickAsync();
        await sample.DispatchClipboardAsync("paste", "abc");
        await Expect(tip).ToHaveTextAsync("Enter a number.");
        await Expect(sample.Page.Locator(".mussel-sheet-sink")).ToHaveValueAsync("abc");
        await sample.Keyboard.PressAsync("Escape");
        await Expect(sample.Cell(2, 1)).ToHaveTextAsync(string.Empty);
    }

    [Fact]
    public async Task ByteLimitCountsFullWidthCharactersAsTwo()
    {
        var sample = await OpenAsync("cell-kinds", "server");

        await sample.Cell(9, 1).ClickAsync();
        await sample.Keyboard.TypeAsync("あいうえお");
        await sample.Keyboard.PressAsync("Enter");
        await Expect(sample.Page.Locator(".mussel-sheet-tip")).ToHaveTextAsync("Enter 8 bytes or fewer (full-width characters count as 2).");
        await sample.Keyboard.PressAsync("Escape");
    }

    [Fact]
    public async Task CtrlEnterInTheReadyStateFillsWithTheActiveCell()
    {
        var sample = await OpenAsync("form", "server");

        await sample.Cell(3, 1).ClickAsync();
        await sample.Keyboard.PressAsync("Shift+ArrowDown");
        await sample.Keyboard.PressAsync("Control+Enter");
        await Expect(sample.Cell(4, 1)).ToHaveTextAsync("2");
        await Expect(sample.Cell(4, 3)).ToHaveTextAsync("4,800");
    }

    [Theory]
    [MemberData(nameof(Modes))]
    public async Task EveryPageOpensFromTheMenu(string mode)
    {
        var sample = await OpenAsync(string.Empty, mode);
        var errors = new List<string>();
        sample.Page.PageError += (_, error) => errors.Add(error);

        var links = sample.Page.Locator("a.mud-nav-link");
        await Expect(links).ToHaveCountAsync(18);
        foreach (var text in await links.AllTextContentsAsync())
        {
            await links.GetByText(text, new() { Exact = true }).ClickAsync();
            await Expect(sample.Page.Locator("h1")).ToContainTextAsync(text);
            await sample.WaitForSheetAsync();
            await Expect(sample.Page.Locator(".sample-source .mud-expand-panel")).Not.ToHaveCountAsync(0);
            await Expect(sample.Page.Locator("a.mud-nav-link.active")).ToHaveTextAsync(text);
        }

        Assert.Empty(errors);
    }

    [Fact]
    public async Task RenderModeSwitchKeepsThePage()
    {
        var sample = opened = await app.OpenAsync("form", "server");
        var active = sample.Page.Locator(".mode-switch a.active");
        await Expect(active).ToHaveTextAsync("Server");

        await sample.Page.Locator(".mode-switch").GetByText("WebAssembly").ClickAsync();
        await Expect(active).ToHaveTextAsync("WebAssembly");
        await Expect(sample.Page.Locator("h1")).ToHaveTextAsync("Form");
        await Expect(sample.Page.Locator(".mode-state")).ToHaveTextAsync("Interactive", new() { Timeout = 30_000 });
        Assert.EndsWith("/form", sample.Page.Url, StringComparison.Ordinal);

        await sample.Cell(3, 1).ClickAsync();
        await sample.Keyboard.TypeAsync("3");
        await sample.Keyboard.PressAsync("Enter");
        await Expect(sample.Cell(3, 3)).ToHaveTextAsync("384,000");

        await sample.Page.Locator(".mud-navmenu").GetByText("Cell Kinds").ClickAsync();
        await Expect(active).ToHaveTextAsync("WebAssembly");
        await sample.Page.Locator(".mode-switch").GetByText("Server").ClickAsync();
        await Expect(active).ToHaveTextAsync("Server");
        Assert.EndsWith("/cell-kinds", sample.Page.Url, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(Modes))]
    public async Task PagesArePrerendered(string mode)
    {
        var sample = opened = await app.OpenBlankAsync(javaScriptEnabled: false);

        await sample.Page.GotoAsync($"{app.BaseAddress}render-mode/{mode}?redirect=%2Fform");
        await Expect(sample.Page.Locator("h1")).ToHaveTextAsync("Form");
        await Expect(sample.Cell(3, 0)).ToHaveTextAsync("Notebook PC");
        await Expect(sample.Cell(8, 3)).ToHaveTextAsync("332,000");
        await Expect(sample.Page.Locator(".mode-state")).ToHaveTextAsync("Prerendered");
        await Expect(sample.Page.Locator(".mode-switch a.active")).ToHaveTextAsync(mode == "wasm" ? "WebAssembly" : "Server");
    }

    [Fact]
    public async Task UnknownAddressShowsTheNotFoundPage()
    {
        var sample = opened = await app.OpenBlankAsync();

        var response = await sample.Page.GotoAsync($"{app.BaseAddress}no-such-page");
        Assert.Equal(404, response?.Status);
        await Expect(sample.Page.Locator("h1")).ToHaveTextAsync("Not found");
        await Expect(sample.Page.Locator(".mud-navmenu")).ToHaveCountAsync(0);

        await sample.Page.GetByRole(AriaRole.Link, new() { Name = "Home" }).ClickAsync();
        await Expect(sample.Page.Locator("h1")).ToHaveTextAsync("Getting Started");
        await sample.WaitForSheetAsync();
    }

    private async Task<SamplePage> OpenAsync(string path, string mode) =>
        opened = await app.OpenAsync(path, ForcedMode ?? mode).ConfigureAwait(true);
}
