namespace MusselSheet;

using System.Text.Json;

using Microsoft.Extensions.DependencyInjection;

using MusselSheet.Internal;

// What a sheet and its view follow: the options the sheet was given, then the registered ones, then the built-in ones
public sealed class SheetOptionsTests : BunitContext
{
    private const string EraText = "令和7年11月5日";

    private const string PlainText = "2025/11/05";

    private const string Rejection = "Enter a date in the form yyyy/MM/dd.";

    private static readonly DateOnly Date = new(2025, 11, 5);

    public SheetOptionsTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public void ASheetUsesTheBuiltInLanguagesWithoutOptions()
    {
        var sheet = EraSheet();

        Assert.Null(sheet.Options);
        Assert.Equal(EraText, sheet.GetText(0, 0));
    }

    [Fact]
    public void ASheetFollowsItsOptionsWithoutAView()
    {
        var sheet = EraSheet();
        sheet.Options = WithoutJapanese();

        Assert.Equal(PlainText, sheet.GetText(0, 0));
        Assert.Equal(PlainText, sheet["A1"].Text);
    }

    [Fact]
    public void ALanguageAnAppAddedWorksWithoutAView()
    {
        var sheet = new Sheet(1, 1) { Culture = TestLanguages.Culture("th-TH"), Options = TestLanguages.Options() };
        sheet.Range("A1").Configure(static c => c.Kind = CellKind.Date);
        sheet.SetValue(0, 0, Date);

        Assert.Equal("5/11/2568", sheet["A1"].Text);
        Assert.Equal("5/11/2568", sheet["A1"].EditText);
    }

    [Fact]
    public void ASheetFromADesignCanBeGivenOptions()
    {
        var loaded = Sheet.FromDesign(EraSheet().ToDesign());
        loaded.SetValue(0, 0, Date);
        Assert.Equal(EraText, loaded.GetText(0, 0));

        loaded.Options = WithoutJapanese();

        Assert.Equal(PlainText, loaded.GetText(0, 0));
    }

    [Fact]
    public void AViewGivesTheRegisteredOptionsToASheetWithoutAnyAndReportsNoChange()
    {
        Services.AddMusselSheet(static o => o.Cultures.Remove("ja"));
        var sheet = EraSheet();
        var changes = 0;
        sheet.Changed += (_, _) => changes++;
        Assert.Equal(EraText, sheet.GetText(0, 0));

        var cut = Render<MusselSheetView>(p => p.Add(x => x.Sheet, sheet));

        Assert.Null(sheet.Options);
        Assert.Same(Services.GetRequiredService<MusselSheetOptions>(), sheet.EffectiveOptions);
        Assert.Equal(PlainText, sheet.GetText(0, 0));
        Assert.Equal(PlainText, Shown(cut));
        Assert.Equal(0, changes);
    }

    [Fact]
    public void AViewGivesTheRegisteredOptionsToTheNextSheetToo()
    {
        Services.AddMusselSheet(static o => o.Cultures.Remove("ja"));
        var next = EraSheet();
        var cut = Render<MusselSheetView>(p => p.Add(x => x.Sheet, EraSheet()));

        cut.Render(p => p.Add(x => x.Sheet, next));

        Assert.Same(Services.GetRequiredService<MusselSheetOptions>(), next.EffectiveOptions);
        Assert.Equal(PlainText, Shown(cut));
    }

    [Fact]
    public async Task ASheetKeepsItsOptionsInAViewWithOthersRegistered()
    {
        Services.AddMusselSheet(static o =>
        {
            o.Cultures.Remove("ja");
            o.Defaults.UndoLimit = 10;
        });
        var own = new MusselSheetOptions { Defaults = { UndoLimit = 20 } };
        var sheet = EraSheet();
        sheet.Options = own;

        var cut = Render<MusselSheetView>(p => p.Add(x => x.Sheet, sheet));

        Assert.Same(own, sheet.Options);
        Assert.Equal(EraText, Shown(cut));
        Assert.Equal(20, cut.Instance.UndoLimit);
        Assert.Null((await Commit(cut, "平成31年4月30日")).Error);
        Assert.Equal(new DateOnly(2019, 4, 30), sheet.GetValue(0, 0));
    }

    [Fact]
    public async Task AViewFollowsItsSheetWithNothingRegistered()
    {
        var own = WithoutJapanese();
        own.Defaults.UndoLimit = 10;
        var sheet = EraSheet();
        sheet.Options = own;

        var cut = Render<MusselSheetView>(p => p.Add(x => x.Sheet, sheet));

        Assert.Same(own, sheet.Options);
        Assert.Equal(10, cut.Instance.UndoLimit);
        Assert.Equal(PlainText, Shown(cut));
        Assert.Equal(Rejection, (await Commit(cut, EraText)).Error);
    }

    [Fact]
    public async Task AViewUsesTheBuiltInLanguagesWithNoOptionsAnywhere()
    {
        var sheet = EraSheet();

        var cut = Render<MusselSheetView>(p => p.Add(x => x.Sheet, sheet));

        Assert.Null(sheet.Options);
        Assert.Equal(EraText, Shown(cut));
        Assert.Null((await Commit(cut, EraText)).Error);
    }

    [Fact]
    public async Task SettingOptionsOnAShownSheetResetsItAndReachesTheBrowser()
    {
        var sheet = EraSheet();
        var cut = Render<MusselSheetView>(p => p.Add(x => x.Sheet, sheet));
        var attached = (JsonElement)JSInterop.Invocations["attach"].Single().Arguments[4]!;
        Assert.NotEqual(0, attached.GetProperty("reading").GetProperty("eras").GetArrayLength());
        var kinds = new List<SheetChangeKind>();
        sheet.Changed += (_, e) => kinds.Add(e.Kind);

        await cut.InvokeAsync(() => sheet.Options = WithoutJapanese());

        Assert.Equal([SheetChangeKind.Reset], kinds);
        await cut.WaitForAssertionAsync(() => Assert.Equal(PlainText, Shown(cut)));
        var sent = (JsonElement)JSInterop.Invocations["setLanguage"][^1].Arguments[0]!;
        Assert.Equal(0, sent.GetProperty("reading").GetProperty("eras").GetArrayLength());
        Assert.Equal(Rejection, (await Commit(cut, EraText)).Error);
    }

    [Fact]
    public void ASheetWhoseOptionsAreClearedGoesBackToTheRegisteredOnes()
    {
        Services.AddMusselSheet(static o => o.Cultures.Remove("ja"));
        var sheet = EraSheet();
        sheet.Options = new MusselSheetOptions();
        var cut = Render<MusselSheetView>(p => p.Add(x => x.Sheet, sheet));
        Assert.Equal(EraText, Shown(cut));

        sheet.Options = null;

        Assert.Equal(PlainText, sheet.GetText(0, 0));
    }

    [Fact]
    public async Task AChangeOfStructureDoesNotSendTheLanguageAgain()
    {
        var sheet = EraSheet();
        var cut = Render<MusselSheetView>(p => p.Add(x => x.Sheet, sheet));
        var maps = JSInterop.Invocations["setMap"].Count;
        var languages = JSInterop.Invocations["setLanguage"].Count;

        await cut.InvokeAsync(() => sheet.Range("A1").Configure(static c => c.Style.Bold = true));

        await cut.WaitForAssertionAsync(() => Assert.Equal(maps + 1, JSInterop.Invocations["setMap"].Count));
        Assert.Equal(languages, JSInterop.Invocations["setLanguage"].Count);
    }

    [Fact]
    public void NewOptionsSendTheirKeysAndMenuToTheBrowser()
    {
        var sheet = EraSheet();
        Render<MusselSheetView>(p => p.Add(x => x.Sheet, sheet));
        var sent = JSInterop.Invocations["setOptions"].Count;
        var options = new MusselSheetOptions { ReadyKeys = { ["Ctrl+K"] = SheetCommands.Edit } };
        options.Menu.Items.Add(new SheetMenuItem { Name = "note", Text = "Note" });

        sheet.Options = options;

        Assert.Equal(sent + 1, JSInterop.Invocations["setOptions"].Count);
        var sentOptions = (JsonElement)JSInterop.Invocations["setOptions"][^1].Arguments[0]!;
        Assert.Contains("Ctrl+K", sentOptions.GetProperty("readyKeys").EnumerateObject().Select(static x => x.Name));
        Assert.Contains("note", sentOptions.GetProperty("menuItems").EnumerateArray().Select(static x => x.GetProperty("value").GetString()));
    }

    [Fact]
    public async Task ChangingTheCultureForgetsTheTypedText()
    {
        var sheet = new Sheet(1, 1) { Culture = TestLanguages.Culture("en-GB") };
        sheet.Range("A1").Configure(static c => c.Kind = CellKind.Date);
        var cut = Render<MusselSheetView>(p => p.Add(x => x.Sheet, sheet));
        await cut.InvokeAsync(() => cut.Instance.CommitAsync(0, 0, "5/11/2025", false));
        Assert.Equal("5/11/2025", sheet.GetInputText(0, 0));

        sheet.Culture = TestLanguages.Culture("en-US");
        await cut.InvokeAsync(() => cut.Instance.CommitAsync(0, 0, sheet.GetEditText(0, 0), false));

        Assert.Null(sheet.GetInputText(0, 0));
        Assert.Equal(Date, sheet.GetValue(0, 0));
    }

    [Fact]
    public async Task ChangingTheOptionsForgetsTheTypedText()
    {
        var sheet = EraSheet();
        sheet.Options = new MusselSheetOptions();
        var cut = Render<MusselSheetView>(p => p.Add(x => x.Sheet, sheet));
        await cut.InvokeAsync(() => cut.Instance.CommitAsync(0, 0, "令和6年11月5日", false));
        Assert.Equal("令和6年11月5日", sheet.GetInputText(0, 0));

        await cut.InvokeAsync(() => sheet.Options = WithoutJapanese());

        Assert.Null(sheet.GetInputText(0, 0));
        Assert.Equal("2024/11/05", sheet.GetEditText(0, 0));
        Assert.Null((await Commit(cut, sheet.GetEditText(0, 0))).Error);
    }

    private static Sheet EraSheet()
    {
        var sheet = new Sheet(1, 1) { Culture = TestLanguages.Culture("ja-JP") };
        sheet.Range("A1").Configure(static c =>
        {
            c.Kind = CellKind.Date;
            c.Format = "ggge年M月d日";
        });
        sheet.SetValue(0, 0, Date);
        return sheet;
    }

    private static MusselSheetOptions WithoutJapanese()
    {
        var options = new MusselSheetOptions();
        options.Cultures.Remove("ja");
        return options;
    }

    private static string Shown(IRenderedComponent<MusselSheetView> cut) =>
        cut.Find(".mussel-sheet-row[data-r='0'] .mussel-sheet-cell[data-c='0']").TextContent;

    private static Task<CommitResult> Commit(IRenderedComponent<MusselSheetView> cut, string text) =>
        cut.InvokeAsync(() => cut.Instance.CommitAsync(0, 0, text, false));
}
