namespace MusselSheet;

using MusselSheet.Internal;

public sealed class OptionsCustomizationTests : BunitContext
{
    public OptionsCustomizationTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public void RegisteredDefaultsApplyUnlessTheSheetSetsTheParameter()
    {
        Services.AddMusselSheet(o =>
        {
            o.Defaults.MoveAfterReturnDirection = SheetMoveDirection.Right;
            o.Defaults.ShowGridLines = false;
            o.Defaults.UndoLimit = 10;
        });

        var plain = Render<MusselSheetView>(p => p.Add(x => x.Sheet, new Sheet(2, 2)));
        Assert.Equal(SheetMoveDirection.Right, plain.Instance.MoveAfterReturnDirection);
        Assert.False(plain.Instance.ShowGridLines);
        Assert.Equal(10, plain.Instance.UndoLimit);
        Assert.Equal(SheetValidationMode.Stay, plain.Instance.ValidationMode);

        var own = Render<MusselSheetView>(p => p.Add(x => x.Sheet, new Sheet(2, 2)).Add(x => x.MoveAfterReturnDirection, SheetMoveDirection.Down));
        Assert.Equal(SheetMoveDirection.Down, own.Instance.MoveAfterReturnDirection);
        Assert.False(own.Instance.ShowGridLines);
    }

    [Theory]
    [InlineData("ctrl+d", "Ctrl+D")]
    [InlineData("shift+ctrl+enter", "Ctrl+Shift+Enter")]
    [InlineData("f4", "F4")]
    [InlineData("Alt+arrowdown", "Alt+ArrowDown")]
    [InlineData("space", "Space")]
    [InlineData("Ctrl+;", "Ctrl+;")]
    public void KeysAreWrittenTheSameWayAsTheScript(string combo, string expected)
    {
        Assert.Equal(expected, KeyCombo.Normalize(combo));
    }

    [Fact]
    public async Task MenuItemClickReachesTheApp()
    {
        Services.AddMusselSheet(o => o.Menu.Items.Add(new SheetMenuItem { Name = "note", Text = "Note", Texts = { ["ja"] = "メモ" } }));
        SheetMenuItemClickedEventArgs? clicked = null;
        var sheet = new Sheet(3, 3);
        var cut = Render<MusselSheetView>(p => p
            .Add(x => x.Sheet, sheet)
            .Add(x => x.MenuItemClicked, e => clicked = e));

        await cut.InvokeAsync(() => cut.Instance.OnMenuItemAsync("note", 0, 0, 1, 2, 1, 1));

        Assert.NotNull(clicked);
        Assert.Equal("note", clicked.Name);
        Assert.Equal(new CellRange(0, 0, 1, 2), clicked.Selection);
        Assert.Equal(new CellAddress(1, 1), clicked.ActiveCell);
        Assert.Same(sheet, clicked.Sheet);

        Assert.Equal("メモ", new SheetMenuItem { Name = "x", Text = "X", Texts = { ["ja"] = "メモ" } }.TextFor(System.Globalization.CultureInfo.GetCultureInfo("ja-JP")));
        Assert.Equal("X", new SheetMenuItem { Name = "x", Text = "X" }.TextFor(System.Globalization.CultureInfo.GetCultureInfo("ja-JP")));
    }
}
