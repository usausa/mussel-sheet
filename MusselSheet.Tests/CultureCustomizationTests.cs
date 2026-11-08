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
        Assert.Same(JapaneseTexts.Default, options.TextsFor(Japanese));
        Assert.Same(SheetTexts.English, options.TextsFor(CultureInfo.GetCultureInfo("fr-FR")));

        Assert.Equal("M/d/yyyy", options.DateFormatFor(English));
        Assert.Equal(DayOfWeek.Monday, options.FirstDayOfWeekFor(CultureInfo.GetCultureInfo("en-GB")));
        options.Cultures["en-GB"] = new SheetCulture { FirstDayOfWeek = DayOfWeek.Sunday };
        Assert.Equal(DayOfWeek.Sunday, options.FirstDayOfWeekFor(CultureInfo.GetCultureInfo("en-GB")));
    }

    [Fact]
    public async Task RegisteredTextsAreUsedForTheUiCulture()
    {
        Services.AddMusselSheet(static o => o.Cultures["ja"].Texts = JapaneseTexts.Default with { Number = "数字だけを入れてください" });
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

    [Theory]
    [InlineData(@"yyyy\/MM\/dd")]
    [InlineData("yyyy\"/\"MM\"/\"dd")]
    [InlineData("yyyy'/'MM'/'dd")]
    [InlineData("yyyy/MM/dd")]
    public void AnEditFormatCanQuoteOrEscapeItsSlash(string format)
    {
        var options = new MusselSheetOptions { Cultures = { ["ja"] = new SheetCulture { EditDateFormat = format } } };
        var sheet = new Sheet(1, 1) { Culture = Japanese, Options = options };
        sheet.Range("A1").Configure(static c => c.Kind = CellKind.Date);
        sheet.SetValue(0, 0, new DateOnly(2025, 11, 5));

        Assert.Equal("2025/11/05", sheet.GetEditText(0, 0));
    }

    [Theory]
    [InlineData("yyyy\"년\" M\"월\" d\"일\"", "2025년 11월 5일")]
    [InlineData(@"yyyy\yM\md\d", "2025y11m5d")]
    public void AFormCanQuoteWithDoubleQuotesOrABackslash(string form, string text)
    {
        var options = new MusselSheetOptions { Cultures = { ["ko"] = new SheetCulture { DateForms = { form } } } };
        var dates = options.DatesFor(CultureInfo.GetCultureInfo("ko-KR"));

        Assert.True(dates.TryRead(text, new DateOnly(2026, 11, 8), out var date));
        Assert.Equal(new DateOnly(2025, 11, 5), date);
    }

    [Fact]
    public void AFormWithAMonthOrDayNameIsLeftOut()
    {
        var options = new MusselSheetOptions { Cultures = { ["en"] = new SheetCulture { DateForms = { "d MMM yyyy", "dddd d/M/yyyy", "d MM yyyy" } } } };

        Assert.Single(options.DatesFor(English).Patterns);
    }

    [Fact]
    public void AnEmptyItemDoesNotCount()
    {
        var options = new MusselSheetOptions { Cultures = { ["ja-JP"] = new SheetCulture { DateFormat = string.Empty, EditDateFormat = string.Empty } } };

        Assert.Equal("yyyy/MM/dd", options.ReadingFor(Japanese).DateFormat);
        Assert.Equal("yyyy'/'MM'/'dd", options.ReadingFor(Japanese).EditDateFormat);
    }

    [Fact]
    public void TheEditTextOfADateBeforeTheFirstEraIsOneEveryCultureReads()
    {
        var bulgarian = CultureInfo.GetCultureInfo("bg-BG");
        var options = new MusselSheetOptions
        {
            Cultures = { ["bg"] = new SheetCulture { Eras = { new SheetEra("N", "N", "N", new DateOnly(1900, 1, 1)) }, EditDateFormat = "ge/M/d", DateForms = { "ge/M/d" } } }
        };
        var sheet = new Sheet(1, 1) { Culture = bulgarian, Options = options };
        sheet.Range("A1").Configure(static c => c.Kind = CellKind.Date);
        sheet.SetValue(0, 0, new DateOnly(1850, 3, 4));
        var reading = options.ReadingFor(sheet.Culture);
        var context = new InputContext(sheet.Culture, new DateOnly(2026, 11, 8), SheetTexts.English, reading.DateFormat, reading);
        var settings = sheet.GetSettings(0, 0);
        var text = sheet.GetEditText(0, 0);

        Assert.Equal("1850/03/04", text);
        Assert.Null(CellInput.Check(CellInput.Normalize(text, settings, reading), settings, context, out var value, out _));
        Assert.Equal(new DateOnly(1850, 3, 4), value);
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
