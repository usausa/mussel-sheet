namespace MusselSheet;

public sealed class CellKindTests
{
    [Fact]
    public void CheckShowsBoxSymbolsOrConfiguredTexts()
    {
        var sheet = new Sheet(1, 2);
        sheet.Configure(CellRange.All, c => c.Kind = CellKind.Checkbox);
        sheet["B1"].Configure(c =>
        {
            c.CheckedText = "済";
            c.UncheckedText = "未";
        });

        Assert.Equal("☐", sheet.GetText(0, 0));
        Assert.Equal("未", sheet.GetText(0, 1));

        sheet.SetValue(0, 0, true);
        sheet.SetValue(0, 1, true);

        Assert.Equal("☑", sheet.GetText(0, 0));
        Assert.Equal("済", sheet.GetText(0, 1));
    }

    [Fact]
    public void CheckTextLeavesTheOtherSideEmpty()
    {
        var sheet = new Sheet(1, 1);
        sheet["A1"].Configure(c =>
        {
            c.Kind = CellKind.Checkbox;
            c.CheckedText = "✓";
        });

        Assert.Equal(string.Empty, sheet.GetText(0, 0));

        sheet.SetValue(0, 0, "true");

        Assert.Equal("✓", sheet.GetText(0, 0));
    }

    [Fact]
    public void ListEditTextIsTheDisplayName()
    {
        var sheet = new Sheet(1, 1);
        sheet["A1"].Configure(c =>
        {
            c.Kind = CellKind.List;
            c.Items = [new CellListItem("S", "営業部")];
        });

        sheet.SetValue(0, 0, "S");

        Assert.Equal("営業部", sheet.GetText(0, 0));
        Assert.Equal("営業部", sheet.GetEditText(0, 0));
    }

    [Fact]
    public void CustomValuesUseTheFormat()
    {
        var sheet = new Sheet(1, 1);
        sheet["A1"].Configure(c =>
        {
            c.Kind = CellKind.Custom;
            c.Format = "N1";
            c.Editor = "rating";
        });

        sheet.SetValue(0, 0, 3.5m);

        Assert.Equal("3.5", sheet.GetText(0, 0));
        Assert.Equal("rating", sheet["A1"].Settings.Editor);
    }

    [Fact]
    public void KindSettingsAreResolvedItemByItem()
    {
        Func<CellSuggestionContext, Task<IReadOnlyList<string>>> provider = _ => Task.FromResult<IReadOnlyList<string>>([]);
        var sheet = new Sheet(2, 2);
        sheet.Configure(CellRange.All, c =>
        {
            c.CheckedText = "済";
            c.Editor = "color";
            c.SuggestionProvider = provider;
        });
        sheet["A1"].Configure(c => c.UncheckedText = "未");

        var a1 = sheet["A1"].Settings;
        var b2 = sheet["B2"].Settings;

        Assert.Equal("済", a1.CheckedText);
        Assert.Equal("未", a1.UncheckedText);
        Assert.Equal("color", a1.Editor);
        Assert.Same(provider, a1.SuggestionProvider);
        Assert.Equal("済", b2.CheckedText);
        Assert.Null(b2.UncheckedText);
    }

    [Fact]
    public void InsertedRowsInheritKindSettings()
    {
        var sheet = new Sheet(1, 1);
        sheet["A1"].Configure(c =>
        {
            c.Kind = CellKind.Checkbox;
            c.CheckedText = "済";
        });

        sheet.InsertRows(1, 1);

        Assert.Equal("済", sheet["A2"].Settings.CheckedText);
    }
}
