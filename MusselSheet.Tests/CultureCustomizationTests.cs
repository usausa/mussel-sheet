namespace MusselSheet;

using MusselSheet.Internal;

public sealed class CultureCustomizationTests : BunitContext
{
    private static readonly CultureInfo Japanese = CultureInfo.GetCultureInfo("ja-JP");

    private static readonly CultureInfo English = CultureInfo.GetCultureInfo("en-US");

    public CultureCustomizationTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public void SettingsAreFoundFromTheCultureToItsParents()
    {
        var options = new MusselSheetOptions { Cultures = { ["ja-JP"] = new SheetCulture { DateFormat = "yyyy年M月d日" } } };

        Assert.Equal("yyyy年M月d日", options.DateFormatFor(Japanese));
        Assert.Same(SheetTexts.Japanese, options.TextsFor(Japanese));
        Assert.Same(SheetTexts.English, options.TextsFor(CultureInfo.GetCultureInfo("fr-FR")));

        Assert.Equal("M/d/yyyy", options.DateFormatFor(English));
        Assert.Equal(DayOfWeek.Monday, options.FirstDayOfWeekFor(CultureInfo.GetCultureInfo("en-GB")));
        options.Cultures["en-GB"] = new SheetCulture { FirstDayOfWeek = DayOfWeek.Sunday };
        Assert.Equal(DayOfWeek.Sunday, options.FirstDayOfWeekFor(CultureInfo.GetCultureInfo("en-GB")));
    }

    [Fact]
    public async Task RegisteredTextsAreUsedForTheUiCulture()
    {
        Services.AddMusselSheet(o => o.Cultures["ja"].Texts = SheetTexts.Japanese with { Number = "数字だけを入れてください" });
        var sheet = new Sheet(1, 1);
        sheet.Range("A1").Configure(c => c.Kind = CellKind.Number);
        var cut = Render<MusselSheetView>(p => p.Add(x => x.Sheet, sheet));

        var result = await cut.InvokeAsync(() => cut.Instance.CommitAsync(0, 0, "abc", false));

        Assert.Equal("数字だけを入れてください", result.Error);
    }

    [Fact]
    public async Task EnglishTextsAreUsedForAnEnglishUiCulture()
    {
        var sheet = new Sheet(1, 2) { Culture = English };
        sheet.Range("A1").Configure(c => c.Kind = CellKind.Number);
        sheet.Range("B1").Configure(c => c.Kind = CellKind.Date);
        var cut = Render<MusselSheetView>(p => p.Add(x => x.Sheet, sheet));

        var previous = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = English;
        try
        {
            Assert.Equal("Enter a number.", (await cut.InvokeAsync(() => cut.Instance.CommitAsync(0, 0, "abc", false))).Error);
            Assert.Equal("Enter a date in the form M/d/yyyy.", (await cut.InvokeAsync(() => cut.Instance.CommitAsync(0, 1, "abc", false))).Error);
        }
        finally
        {
            CultureInfo.CurrentUICulture = previous;
        }
    }

    [Fact]
    public void DateDisplayAndCalendarFollowTheSheetCulture()
    {
        var sheet = new Sheet(1, 1) { Culture = English };
        sheet.Range("A1").Configure(c => c.Kind = CellKind.Date);
        sheet.SetValue(0, 0, new DateOnly(2025, 11, 5));

        Assert.Equal("11/5/2025", sheet.GetText(0, 0));
        sheet.Culture = Japanese;
        Assert.Equal("2025/11/05", sheet.GetText(0, 0));

        var calendar = CalendarInfo.From(Japanese, DayOfWeek.Sunday);
        Assert.Equal("日", calendar.DayNames[0]);
        Assert.Equal("yyyy年M月", calendar.YearMonth);
        Assert.Equal("November", CalendarInfo.From(English, DayOfWeek.Sunday).MonthNames[10]);
        Assert.Equal(1, CalendarInfo.From(English, DayOfWeek.Monday).FirstDay);
    }
}
