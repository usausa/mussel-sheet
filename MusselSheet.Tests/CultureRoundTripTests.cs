namespace MusselSheet;

using MusselSheet.Internal;

// Every culture .NET knows must read back what the sheet itself writes, with no language registered. Nothing here
// names a culture, so a culture that writes something new fails here first.
public sealed class CultureRoundTripTests
{
    private static readonly DateOnly Today = new(2026, 11, 6);

    public static TheoryData<string> Cultures()
    {
        var data = new TheoryData<string>();
        foreach (var culture in CultureInfo.GetCultures(CultureTypes.AllCultures).Where(static x => x.Name.Length > 0))
        {
            data.Add(culture.Name);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(Cultures))]
    public void NumbersAreReadBack(string name)
    {
        var (culture, context) = Create(name);
        var number = new ResolvedCellSettings { Kind = CellKind.Number };
        foreach (var value in new[] { 1234.5m, -1234.5m, 0.25m })
        {
            AssertReadBack(CellText.Edit(value, number, culture, context.Reading), number, context, value);
        }

        AssertReadBack(culture.NumberFormat.NegativeSign + "5", number, context, -5m);
        AssertReadBack("－１２３", number, context, -123m);
        AssertReadBack("−５", number, context, -5m);
    }

    [Theory]
    [MemberData(nameof(Cultures))]
    public void DatesAreReadBackAsTheSameDay(string name)
    {
        var (culture, context) = Create(name);
        var date = new ResolvedCellSettings { Kind = CellKind.Date };
        foreach (var value in new[] { new DateOnly(2025, 11, 5), new DateOnly(1999, 12, 31), new DateOnly(2000, 2, 29), new DateOnly(2026, 1, 2) })
        {
            AssertReadBack(CellText.Edit(value, date, culture, context.Reading), date, context, value);
        }

        AssertReadBack("２０２５／１１／５", date, context, new DateOnly(2025, 11, 5));
    }

    [Theory]
    [MemberData(nameof(Cultures))]
    public void TimesAreReadBack(string name)
    {
        var (culture, context) = Create(name);
        var time = new ResolvedCellSettings { Kind = CellKind.Time };
        foreach (var value in new[] { new TimeOnly(12, 30), new TimeOnly(7, 5, 9) })
        {
            AssertReadBack(CellText.Edit(value, time, culture, context.Reading), time, context, value);
        }

        AssertReadBack("12:30", time, context, new TimeOnly(12, 30));
    }

    [Theory]
    [MemberData(nameof(Cultures))]
    public void ASheetKeepsItsCultureOnTheGregorianCalendar(string name)
    {
        var source = CultureInfo.GetCultureInfo(name);
        var sheet = new Sheet(1, 1) { Culture = source };

        Assert.IsType<GregorianCalendar>(sheet.Culture.DateTimeFormat.Calendar);
        Assert.Equal(source.Name, sheet.Culture.Name);
        if (source.DateTimeFormat.Calendar is GregorianCalendar)
        {
            Assert.Same(source, sheet.Culture);
        }
    }

    [Fact]
    public void ASheetKeepsTheDateSettingsAnAppGaveACultureWithAnotherCalendar()
    {
        var culture = (CultureInfo)CultureInfo.GetCultureInfo("th-TH").Clone();
        culture.DateTimeFormat.ShortDatePattern = "yyyy-MM-dd";
        culture.DateTimeFormat.DateSeparator = "-";
        var sheet = new Sheet(1, 1) { Culture = culture };
        sheet.Range("A1").Configure(static c => c.Kind = CellKind.Date);
        sheet.SetValue(0, 0, new DateOnly(2025, 11, 5));

        Assert.IsType<GregorianCalendar>(sheet.Culture.DateTimeFormat.Calendar);
        Assert.Equal("yyyy-MM-dd", sheet.Culture.DateTimeFormat.ShortDatePattern);
        Assert.Equal("2025-11-05", sheet.GetText(0, 0));
        Assert.Equal(CultureInfo.GetCultureInfo("th-TH").DateTimeFormat.AbbreviatedDayNames, sheet.Culture.DateTimeFormat.AbbreviatedDayNames);
    }

    [Fact]
    public void ASheetTakesItsCultureAsSetAndAgainWhenItIsSetAgain()
    {
        var culture = (CultureInfo)CultureInfo.GetCultureInfo("en-US").Clone();
        var sheet = new Sheet(1, 1) { Culture = culture };
        var options = new MusselSheetOptions();
        var before = options.ReadingFor(sheet.Culture);

        culture.NumberFormat.NumberDecimalSeparator = ",";
        culture.NumberFormat.NumberGroupSeparator = ".";
        Assert.True(sheet.Culture.IsReadOnly);
        Assert.Equal(".", sheet.Culture.NumberFormat.NumberDecimalSeparator);

        sheet.Culture = culture;
        var after = options.ReadingFor(sheet.Culture);

        Assert.Equal(".", before.DecimalSeparator);
        Assert.Equal(",", after.DecimalSeparator);
        Assert.Equal(".", after.GroupSeparator);
    }

    [Theory]
    [InlineData("ru-RU", "ноябрь")]
    [InlineData("ja-JP", "11月")]
    public void TheCalendarTitleUsesTheNamesNetUsesForIt(string name, string month)
    {
        var culture = ReadingRules.Gregorian(CultureInfo.GetCultureInfo(name));
        var calendar = CalendarInfo.From(culture, DayOfWeek.Sunday);

        Assert.Equal(month, calendar.MonthNames[10]);
        Assert.Equal(culture.DateTimeFormat.GetEraName(1), calendar.Era);
        Assert.Contains(month, new DateTime(2025, 11, 1).ToString(calendar.YearMonth, culture), StringComparison.Ordinal);
    }

    [Fact]
    public void TheCalendarNamesTheGregorianMonths()
    {
        var sheet = new Sheet(1, 1) { Culture = CultureInfo.GetCultureInfo("fa-IR") };
        sheet.Range("A1").Configure(static c => c.Kind = CellKind.Date);
        sheet.SetValue(0, 0, new DateOnly(2025, 11, 5));

        var calendar = CalendarInfo.From(sheet.Culture, DayOfWeek.Sunday);

        Assert.IsType<GregorianCalendar>(sheet.Culture.DateTimeFormat.Calendar);
        Assert.Contains("2025", sheet.GetText(0, 0), StringComparison.Ordinal);
        Assert.NotEqual(CalendarInfo.From(CultureInfo.GetCultureInfo("fa-IR"), DayOfWeek.Sunday).MonthNames[10], calendar.MonthNames[10]);
    }

    private static (CultureInfo Culture, InputContext Context) Create(string name)
    {
        var culture = ReadingRules.Gregorian(CultureInfo.GetCultureInfo(name));
        return (culture, new InputContext(culture, Today, SheetTexts.English, culture.DateTimeFormat.ShortDatePattern, ReadingRules.From(culture)));
    }

    private static void AssertReadBack(string text, ResolvedCellSettings settings, InputContext context, object expected)
    {
        var normalized = CellInput.Normalize(text, settings, context.Reading);

        Assert.True(CellInput.TryConvert(normalized, settings, context, out var value, out _), $"[{text}] was rejected");
        Assert.Equal(expected, value);
    }
}
