namespace MusselSheet;

using MusselSheet.Internal;

public sealed class ValueFormTests
{
    [Fact]
    public void OtherDateAndTimeTypesAreShownInDateAndTimeCells()
    {
        var sheet = new Sheet(4, 2);
        sheet.Range("A:A").Configure(static c => c.Kind = CellKind.Date);
        sheet.Range("B:B").Configure(static c => c.Kind = CellKind.Time);
        sheet.SetValues(0, 0,
        [
            [new DateTime(2026, 11, 5, 9, 0, 0, DateTimeKind.Unspecified), new TimeSpan(9, 30, 0)],
            [new DateTimeOffset(2026, 11, 6, 0, 0, 0, TimeSpan.Zero), new TimeSpan(9, 30, 15)],
            ["Not a date", new DateTime(2026, 1, 1, 10, 15, 30, DateTimeKind.Unspecified)],
            [null, "Not a time"]
        ]);

        Assert.Equal("2026/11/05", sheet.GetText(0, 0));
        Assert.Equal("09:30", sheet.GetText(0, 1));
        Assert.Equal("2026/11/06", sheet.GetText(1, 0));
        Assert.Equal("09:30", sheet.GetText(1, 1));
        Assert.Equal("09:30:15", sheet.GetEditText(1, 1));
        Assert.Equal("Not a date", sheet.GetText(2, 0));
        Assert.Equal("10:15", sheet.GetText(2, 1));
        Assert.Equal("10:15:30", sheet.GetEditText(2, 1));
        Assert.Equal("Not a time", sheet.GetText(3, 1));
    }

    [Theory]
    [InlineData(CellKind.Date, "yyyy/MM/dd HH:mm", "2025/11/05 09:30")]
    [InlineData(CellKind.Date, "G", "2025/11/05 9:30:00")]
    [InlineData(CellKind.Time, "yyyy HH:mm", "9:30")]
    [InlineData(CellKind.Number, "Q9999999999", "1.5")]
    public void AFormatTheValueCannotTakeFallsBackToItsGeneralForm(CellKind kind, string format, string expected)
    {
        var sheet = new Sheet(1, 1) { Culture = CultureInfo.GetCultureInfo("ja-JP") };
        sheet.Range("A1").Configure(c =>
        {
            c.Kind = kind;
            c.Format = format;
            if (kind == CellKind.Number)
            {
                c.Minimum = 0m;
            }
        });
        sheet.SetValue(0, 0, kind == CellKind.Number ? 1.5m : new DateTime(2025, 11, 5, 9, 30, 0, DateTimeKind.Unspecified));

        Assert.Equal(expected, sheet.GetText(0, 0));
        Assert.NotNull(SheetMapBuilder.Build(sheet, 24, 40, SheetValidationMode.Stay));
    }

    [Fact]
    public void ADateOnlyWithATimeFormatShowsItsShortDate()
    {
        var sheet = new Sheet(1, 1) { Culture = CultureInfo.GetCultureInfo("ja-JP") };
        sheet.Range("A1").Configure(static c =>
        {
            c.Kind = CellKind.Date;
            c.Format = "yyyy/MM/dd HH:mm";
        });
        sheet.SetValue(0, 0, new DateOnly(2025, 11, 5));

        Assert.Equal("2025/11/05", sheet.GetText(0, 0));
    }

    [Fact]
    public void ValuesAreConvertedBetweenDateAndTimeTypes()
    {
        Assert.Equal(new DateOnly(2026, 11, 5), CellText.ConvertTo(new DateTime(2026, 11, 5, 1, 2, 3, DateTimeKind.Unspecified), typeof(DateOnly)));
        Assert.Equal(new DateTime(2026, 11, 5, 0, 0, 0, DateTimeKind.Unspecified), CellText.ConvertTo(new DateOnly(2026, 11, 5), typeof(DateTime)));
        Assert.Equal(new TimeOnly(9, 30), CellText.ConvertTo(new TimeSpan(9, 30, 0), typeof(TimeOnly)));
        Assert.Equal(new TimeSpan(9, 30, 0), CellText.ConvertTo(new TimeOnly(9, 30), typeof(TimeSpan)));
        Assert.Null(CellText.ConvertTo(new object(), typeof(int)));
    }

    [Fact]
    public void OtherValueTypesAreSavedInTheDesign()
    {
        var sheet = new Sheet(1, 6);
        sheet.SetValues(0, 0, [[new DateTime(2026, 11, 5, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(9, 30, 0), 1.5d, 2.5f, 7, new Uri("https://example.com/")]]);

        var values = sheet.ToDesign(includeValues: true).Values!;

        Assert.Equal(("date", "2026-11-05"), Saved(values, "A1"));
        Assert.Equal(("time", "09:30:00"), Saved(values, "B1"));
        Assert.Equal(("number", "1.5"), Saved(values, "C1"));
        Assert.Equal(("number", "2.5"), Saved(values, "D1"));
        Assert.Equal(("number", "7"), Saved(values, "E1"));
        Assert.Equal(("text", "https://example.com/"), Saved(values, "F1"));
    }

    [Theory]
    [InlineData("en-GB", "dmy")]
    [InlineData("en-US", "mdy")]
    [InlineData("ja-JP", "ymd")]
    public void DateOrderComesFromTheCulture(string culture, string expected)
    {
        Assert.Equal(expected, ReadingRules.From(CultureInfo.GetCultureInfo(culture)).ForScript().Order);
    }

    [Theory]
    [InlineData("d MMM yyyy", "dd/MM/yyyy")]
    [InlineData("MMM d, yyyy", "MM/dd/yyyy")]
    [InlineData("yyyy MMM d", "yyyy/MM/dd")]
    [InlineData("yy.M.d", "yyyy.M.d")]
    public void EditFormatCanBeReadBack(string pattern, string expected)
    {
        var culture = (CultureInfo)CultureInfo.InvariantCulture.Clone();
        culture.DateTimeFormat.ShortDatePattern = pattern;

        Assert.Equal(expected, ReadingRules.EditFormat(culture));
    }

    [Theory]
    [InlineData("ggge'年'M\"月\"d\\日", true, "令和8年11月5日")]
    [InlineData("'ggge'", false, "ggge")]
    [InlineData("\"ge\"", false, "ge")]
    [InlineData("\\g\\e", false, "ge")]
    [InlineData("ge\\", true, "R8")]
    public void TextInEraFormatsIsKeptAsIs(string format, bool isEra, string expected)
    {
        var date = new DateOnly(2026, 11, 5);

        var culture = CultureInfo.GetCultureInfo("ja-JP");

        Assert.Equal(isEra, DateRules.IsEraFormat(format));
        Assert.Equal(expected, MusselSheetOptions.Default.DatesFor(culture).Format(date, format, culture));
    }

    private static (string Type, string Value) Saved(IEnumerable<CellValueDesign> values, string cell)
    {
        var value = values.Single(x => x.Cell == cell);
        return (value.Type, value.Value);
    }
}
