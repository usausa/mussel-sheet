namespace MusselSheet;

using MusselSheet.Internal;

public sealed class CultureReadingTests
{
    private static readonly DateOnly Today = new(2026, 11, 2);

    private static readonly CultureInfo English = CultureInfo.GetCultureInfo("en-US");

    private static readonly CultureInfo German = CultureInfo.GetCultureInfo("de-DE");

    [Fact]
    public void ReadingRulesComeFromTheCulture()
    {
        var japanese = ReadingRules.From(CultureInfo.GetCultureInfo("ja-JP"), true);
        Assert.Equal(DateOrder.YearMonthDay, japanese.DateOrder);
        Assert.Equal("yyyy/MM/dd", japanese.EditDateFormat);

        var english = ReadingRules.From(English, false);
        Assert.Equal(DateOrder.MonthDayYear, english.DateOrder);
        Assert.Equal("M/d/yyyy", english.EditDateFormat);

        var german = ReadingRules.From(German, false);
        Assert.Equal(DateOrder.DayMonthYear, german.DateOrder);
        Assert.Equal(",", german.DecimalSeparator);
        Assert.Equal(".", german.GroupSeparator);
        Assert.Equal("dd.MM.yyyy", german.EditDateFormat);
    }

    [Theory]
    [InlineData("en-US", "11/5/2025", 2025, 11, 5)]
    [InlineData("en-US", "11/5/25", 2025, 11, 5)]
    [InlineData("en-US", "11/5", 2026, 11, 5)]
    [InlineData("en-US", "11052025", 2025, 11, 5)]
    [InlineData("en-US", "2025-11-05", 2025, 11, 5)]
    [InlineData("de-DE", "05.11.2025", 2025, 11, 5)]
    [InlineData("de-DE", "5.11", 2026, 11, 5)]
    [InlineData("de-DE", "0511", 2026, 11, 5)]
    public void DatesAreReadInTheCultureOrder(string culture, string text, int year, int month, int day)
    {
        Assert.True(CellInput.TryConvert(text, Date, Context(culture), out var value, out _));
        Assert.Equal(new DateOnly(year, month, day), value);
    }

    [Fact]
    public void JapaneseDateFormsAreOnlyForJapaneseReading()
    {
        var context = Context("en-US");
        Assert.False(CellInput.TryConvert("11月5日", Date, context, out _, out _));
        Assert.False(CellInput.TryConvert(CellInput.Normalize("2025・11・5", Date, context.Reading), Date, context, out _, out _));
    }

    [Theory]
    [InlineData("en-US", "1,234.5", 1234.5)]
    [InlineData("de-DE", "1.234,5", 1234.5)]
    [InlineData("de-DE", "1,5", 1.5)]
    [InlineData("de-DE", ",5", 0.5)]
    [InlineData("fr-FR", "1 234,5", 1234.5)]
    public void NumbersAreReadWithTheCultureSeparators(string culture, string text, double expected)
    {
        Assert.True(CellInput.TryConvert(text, Number, Context(culture), out var value, out _));
        Assert.Equal((decimal)expected, value);
    }

    [Fact]
    public void EditTextCanBeReadBackInTheSameCulture()
    {
        var sheet = new Sheet(1, 2) { Culture = German };
        sheet.Range("A1").Configure(c => c.Kind = CellKind.Date);
        sheet.Range("B1").Configure(c => c.Kind = CellKind.Number);
        sheet.SetValues(0, 0, [[new DateOnly(2025, 11, 5), 1.5m]]);

        Assert.Equal("05.11.2025", sheet.GetEditText(0, 0));
        Assert.Equal("1,5", sheet.GetEditText(0, 1));
        Assert.True(CellInput.TryConvert(sheet.GetEditText(0, 1), Number, Context("de-DE"), out var number, out _));
        Assert.Equal(1.5m, number);
    }

    private static ResolvedCellSettings Date => new() { Kind = CellKind.Date };

    private static ResolvedCellSettings Number => new() { Kind = CellKind.Number };

    private static InputContext Context(string name)
    {
        var culture = CultureInfo.GetCultureInfo(name);
        return new InputContext(culture, Today, SheetTexts.English, culture.DateTimeFormat.ShortDatePattern, ReadingRules.From(culture, false));
    }
}
