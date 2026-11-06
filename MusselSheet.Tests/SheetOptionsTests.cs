namespace MusselSheet;

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

        Assert.Same(Services.GetRequiredService<MusselSheetOptions>(), sheet.Options);
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

        Assert.Same(Services.GetRequiredService<MusselSheetOptions>(), next.Options);
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
        Assert.NotEmpty(SheetMapBuilder.Build(sheet, 24, 40, SheetValidationMode.Stay, cut.Instance.InputContext).Reading.Eras);
        var kinds = new List<SheetChangeKind>();
        sheet.Changed += (_, e) => kinds.Add(e.Kind);

        await cut.InvokeAsync(() => sheet.Options = WithoutJapanese());

        Assert.Equal([SheetChangeKind.Reset], kinds);
        await cut.WaitForAssertionAsync(() => Assert.Equal(PlainText, Shown(cut)));
        Assert.Empty(SheetMapBuilder.Build(sheet, 24, 40, SheetValidationMode.Stay, cut.Instance.InputContext).Reading.Eras);
        Assert.Equal(Rejection, (await Commit(cut, EraText)).Error);
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
