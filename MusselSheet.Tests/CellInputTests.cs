namespace MusselSheet;

using MusselSheet.Internal;

public sealed class CellInputTests
{
    private static readonly CultureInfo Japanese = CultureInfo.GetCultureInfo("ja-JP");

    private static readonly DateOnly Today = new(2026, 11, 2);

    private static readonly InputContext Context = new(Japanese, Today, JapaneseTexts.Default, "yyyy/MM/dd", MusselSheetOptions.Default.ReadingFor(Japanese));

    [Theory]
    [InlineData("fullwidth", "abc", "全角で入力してください")]
    [InlineData("halfwidth", "あ", "半角で入力してください")]
    [InlineData("hiragana", "カ", "ひらがなで入力してください")]
    [InlineData("katakana", "か", "全角カタカナで入力してください")]
    [InlineData("halfwidthKatakana", "カ", "半角カタカナで入力してください")]
    [InlineData("alphanumeric", "あ", "半角英数字で入力してください")]
    [InlineData("digits", "a", "半角数字で入力してください")]
    public void EveryRestrictionHasItsMessage(string restriction, string text, string expected)
    {
        var settings = Settings(CellKind.Text) with { CharacterRestriction = TestLanguages.Set(restriction) };

        Assert.Equal(expected, CellInput.Check(text, settings, Context, out _, out _));
    }

    [Fact]
    public void TimeLimitsAreChecked()
    {
        var settings = Settings(CellKind.Time) with { Minimum = new TimeOnly(9, 0), Maximum = new TimeOnly(17, 0) };

        Assert.Null(CellInput.Check("10:00", settings, Context, out _, out _));
        Assert.Equal("09:00 から 17:00 の範囲で入力してください", CellInput.Check("08:00", settings, Context, out _, out _));
    }

    [Fact]
    public void ACheckboxTakesOnlyTrueAndFalse()
    {
        var settings = Settings(CellKind.Checkbox);

        Assert.Null(CellInput.Check("TRUE", settings, Context, out var value, out _));
        Assert.True(value is true);
        Assert.Equal(JapaneseTexts.Default.Check, CellInput.Check("abc", settings, Context, out _, out _));
    }

    [Theory]
    [InlineData("１，２３４", CellKind.Number, "1,234")]
    [InlineData("ー５", CellKind.Number, "-5")]
    [InlineData("　２０２６／１１／０１ ", CellKind.Date, "2026/11/01")]
    [InlineData("２０２６・１２・２４", CellKind.Date, "2026/12/24")]
    [InlineData("１１月５日", CellKind.Date, "11月5日")]
    [InlineData("1・5", CellKind.Number, "1・5")]
    [InlineData("９：０５", CellKind.Time, "9:05")]
    [InlineData("　あ　", CellKind.Text, "　あ　")]
    [InlineData("  営業部 ", CellKind.List, "営業部")]
    public void NormalizeMatchesTheScript(string text, CellKind kind, string expected)
    {
        Assert.Equal(expected, CellInput.Normalize(text, kind, Context.Reading));
    }

    [Theory]
    [InlineData("1,234", 1234)]
    [InlineData("-1.5", -1.5)]
    [InlineData(".5", 0.5)]
    [InlineData("+7", 7)]
    public void NumberAcceptsSeparatorsAndSigns(string text, double expected)
    {
        Assert.True(CellInput.TryConvert(text, Settings(CellKind.Number), Context, out var value, out _));
        Assert.Equal((decimal)expected, value);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("5.")]
    [InlineData("-")]
    [InlineData(",1")]
    public void NumberRejectsOtherText(string text)
    {
        Assert.False(CellInput.TryConvert(text, Settings(CellKind.Number), Context, out _, out var error));
        Assert.Equal(JapaneseTexts.Default.Number, error);
    }

    [Fact]
    public void EmptyTextHasNoValue()
    {
        Assert.True(CellInput.TryConvert(string.Empty, Settings(CellKind.Number), Context, out var value, out var error));
        Assert.Null(value);
        Assert.Null(error);
    }

    [Fact]
    public void DatesAndTimesMustExist()
    {
        Assert.True(CellInput.TryConvert("2026/1/5", Settings(CellKind.Date), Context, out var date, out _));
        Assert.Equal(new DateOnly(2026, 1, 5), date);
        Assert.True(CellInput.TryConvert("2024-02-29", Settings(CellKind.Date), Context, out _, out _));
        Assert.False(CellInput.TryConvert("2026/02/30", Settings(CellKind.Date), Context, out _, out _));
        Assert.False(CellInput.TryConvert("2100/02/29", Settings(CellKind.Date), Context, out _, out _));
        Assert.True(CellInput.TryConvert("9:05:30", Settings(CellKind.Time), Context, out var time, out _));
        Assert.Equal(new TimeOnly(9, 5, 30), time);
        Assert.False(CellInput.TryConvert("24:00", Settings(CellKind.Time), Context, out _, out var error));
        Assert.Equal(JapaneseTexts.Default.Time, error);
    }

    // Two-digit years follow Excel: 00-29 are 2000s, 30-99 are 1900s
    [Theory]
    [InlineData("11/5", 2026, 11, 5)]
    [InlineData("1-5", 2026, 1, 5)]
    [InlineData("11.5", 2026, 11, 5)]
    [InlineData("25/11/5", 2025, 11, 5)]
    [InlineData("29-1-1", 2029, 1, 1)]
    [InlineData("30.1.1", 1930, 1, 1)]
    [InlineData("1105", 2026, 11, 5)]
    [InlineData("251105", 2025, 11, 5)]
    [InlineData("20251105", 2025, 11, 5)]
    [InlineData("11月5日", 2026, 11, 5)]
    [InlineData("2025年11月5日", 2025, 11, 5)]
    [InlineData("25年1月15日", 2025, 1, 15)]
    public void DateShorthandFillsTheYear(string text, int year, int month, int day)
    {
        Assert.True(CellInput.TryConvert(text, Settings(CellKind.Date), Context, out var value, out _));
        Assert.Equal(new DateOnly(year, month, day), value);
    }

    [Theory]
    [InlineData("5")]
    [InlineData("115")]
    [InlineData("1/2/3")]
    [InlineData("2/29")]
    [InlineData("1305")]
    [InlineData("11月5")]
    [InlineData("2025/11")]
    public void DateShorthandRejectsOtherText(string text)
    {
        Assert.False(CellInput.TryConvert(text, Settings(CellKind.Date), Context, out _, out var error));
        Assert.Equal(Context.DateMessage, error);
    }

    [Fact]
    public void OnlyYearlessDatesOmitTheYear()
    {
        Assert.True(CellInput.OmitsYear("11/5", Context.Reading));
        Assert.True(CellInput.OmitsYear("1105", Context.Reading));
        Assert.True(CellInput.OmitsYear("11月5日", Context.Reading));
        Assert.False(CellInput.OmitsYear("25/11/5", Context.Reading));
        Assert.False(CellInput.OmitsYear("20251105", Context.Reading));
        Assert.False(CellInput.OmitsYear("2025年11月5日", Context.Reading));
        Assert.False(CellInput.OmitsYear("abc", Context.Reading));
    }

    [Fact]
    public void ListMatchesValueOrDisplayText()
    {
        var settings = Settings(CellKind.List) with { Items = [new CellListItem("S", "営業部"), "総務部"] };

        Assert.True(CellInput.TryConvert("営業部", settings, Context, out var value, out _));
        Assert.Equal("S", value);
        Assert.True(CellInput.TryConvert("s", settings, Context, out value, out _));
        Assert.Equal("S", value);
        Assert.False(CellInput.TryConvert("開発部", settings, Context, out _, out var error));
        Assert.Equal(JapaneseTexts.Default.List, error);
    }

    [Fact]
    public void ValidateChecksRequiredRangeLengthAndPattern()
    {
        Assert.Equal(JapaneseTexts.Default.Required, CellInput.Validate(null, string.Empty, Settings(CellKind.Text) with { Required = true }, Context));
        var range = Settings(CellKind.Number) with { Minimum = 0, Maximum = 100, Format = "N0" };
        Assert.Equal("0 から 100 の範囲で入力してください", CellInput.Validate(101m, "101", range, Context));
        Assert.Null(CellInput.Validate(100m, "100", range, Context));
        Assert.Equal("1,000 以下で入力してください", CellInput.Validate(1001m, "1001", Settings(CellKind.Number) with { Maximum = 1000, Format = "N0" }, Context));
        Assert.Equal("2026/01/01 以上で入力してください", CellInput.Validate(new DateOnly(2025, 12, 31), "2025/12/31", Settings(CellKind.Date) with { Minimum = new DateOnly(2026, 1, 1) }, Context));
        Assert.Equal("2 文字以内で入力してください", CellInput.Validate("𠮷野家", "𠮷野家", Settings(CellKind.Text) with { MaxLength = 2 }, Context));
        Assert.Null(CellInput.Validate("𠮷野家", "𠮷野家", Settings(CellKind.Text) with { MaxLength = 3 }, Context));
        Assert.Equal("4 バイト以内で入力してください(全角は2バイト)", CellInput.Validate("あいう", "あいう", Settings(CellKind.Text) with { MaxByteLength = 4 }, Context));
        Assert.Equal(JapaneseTexts.Default.Pattern, CellInput.Validate("1234567", "1234567", Settings(CellKind.Text) with { Pattern = "[0-9]{3}-[0-9]{4}" }, Context));
        Assert.Null(CellInput.Validate("abc", "abc", Settings(CellKind.Text) with { Pattern = "(" }, Context));
    }

    [Fact]
    public void CheckUsesTheCustomMessageForEveryError()
    {
        var settings = Settings(CellKind.Number) with { ErrorMessage = "0〜100 で入力してください", Minimum = 0, Maximum = 100 };

        Assert.Equal("0〜100 で入力してください", CellInput.Check("abc", settings, Context, out _, out var converted));
        Assert.False(converted);
        Assert.Equal("0〜100 で入力してください", CellInput.Check("200", settings, Context, out var value, out converted));
        Assert.True(converted);
        Assert.Equal(200m, value);
    }

    [Theory]
    [InlineData("ｱｲｳ", 3)]
    [InlineData("あい", 4)]
    [InlineData("A1あ", 4)]
    [InlineData("𠮷", 2)]
    public void CountBytesTreatsFullwidthAsTwo(string text, int expected)
    {
        Assert.Equal(expected, CellInput.CountBytes(text));
    }

    [Theory]
    [InlineData("ヤマダ　タロウ・ー", "katakana", true)]
    [InlineData("ヤマダ太郎", "katakana", false)]
    [InlineData("やまだ ー", "hiragana", true)]
    [InlineData("ﾔﾏﾀﾞ", "halfwidthKatakana", true)]
    [InlineData("ＡＢＣあ", "fullwidth", true)]
    [InlineData("ABCあ", "fullwidth", false)]
    [InlineData("ABC ﾔ", "halfwidth", true)]
    [InlineData("A1b2", "alphanumeric", true)]
    [InlineData("A1-2", "alphanumeric", false)]
    [InlineData("0123", "digits", true)]
    [InlineData("12a", "digits", false)]
    [InlineData("何でも", "none", true)]
    public void RestrictionsMatchTheScript(string text, string restriction, bool expected)
    {
        Assert.Equal(expected, CellInput.IsAllowed(text, TestLanguages.Set(restriction)));
    }

    [Fact]
    public void RestrictionIsCheckedOnlyForTextKinds()
    {
        var kana = Settings(CellKind.Text) with { CharacterRestriction = JapaneseCharacters.Katakana };

        Assert.Equal("やまだ ﾀﾛｳ", CellInput.Normalize("やまだ ﾀﾛｳ", kana, Context.Reading));
        Assert.Null(CellInput.Check("ヤマダ タロウ", kana, Context, out _, out _));
        Assert.Equal(JapaneseTexts.Default.CharacterSets["katakana"], CellInput.Check("山田", kana, Context, out _, out _));
        Assert.Equal(JapaneseTexts.Default.CharacterSets["digits"], CellInput.Check("12a", Settings(CellKind.Text) with { CharacterRestriction = SheetCharacters.Digits }, Context, out _, out _));
        Assert.Null(CellInput.Check("12", Settings(CellKind.Number) with { CharacterRestriction = JapaneseCharacters.Katakana }, Context, out _, out _));
    }

    [Theory]
    [InlineData("1234567", "999-9999", "123-4567")]
    [InlineData("123-4567", "999-9999", "123-4567")]
    [InlineData("123 4567", "999-9999", "123-4567")]
    [InlineData("12-34567", "999-9999", null)]
    [InlineData("123456", "999-9999", null)]
    [InlineData("12345678", "999-9999", null)]
    [InlineData("AB12", "AA-99", "AB-12")]
    [InlineData("A112", "X-X99", "A-112")]
    [InlineData("12", "\\9-99", "9-12")]
    public void FormatMaskMatchesTheScript(string text, string mask, string? expected)
    {
        Assert.Equal(expected, CellInput.FormatMask(text, mask));
    }

    [Fact]
    public void MaskCellsAreNormalizedAndValidated()
    {
        var zip = Settings(CellKind.Mask) with { Mask = "999-9999" };

        Assert.Equal("123-4567", CellInput.Normalize("１２３４５６７", zip, Context.Reading));
        Assert.Null(CellInput.Check(CellInput.Normalize("１２３ー４５６７", zip, Context.Reading), zip, Context, out var value, out _));
        Assert.Equal("123-4567", value);
        Assert.Equal("999-9999 の形で入力してください", CellInput.Check("12345", zip, Context, out _, out _));
        Assert.Null(CellInput.Check(string.Empty, zip, Context, out _, out _));
        Assert.Equal("9-99", CellInput.MaskDisplay("\\9-99"));

        Assert.Equal("999", CellInput.ActiveMask(Settings(CellKind.Text) with { Mask = "999" }));
        Assert.Null(CellInput.ActiveMask(Settings(CellKind.Number) with { Mask = "999" }));
    }

    private static ResolvedCellSettings Settings(CellKind kind) => ResolvedCellSettings.Default with { Kind = kind };

    [Theory]
    [InlineData(@"\w+", "abc_1", true)]
    [InlineData(@"\w+", "あいう", false)]
    [InlineData(@"\d+", "123", true)]
    [InlineData(@"\d+", "๑๒๓", false)]
    [InlineData(@"\d+", "１２３", false)]
    [InlineData("abc", "abc\n", false)]
    [InlineData(@"\p{IsHiragana}+", "abc", true)]
    public void APatternIsReadAsTheBrowserReadsIt(string pattern, string text, bool allowed)
    {
        var settings = Settings(CellKind.Text) with { Pattern = pattern };

        Assert.Equal(allowed, CellInput.Validate(text, text, settings, Context) is null);
        Assert.Equal(pattern == @"\p{IsHiragana}+" ? null : pattern, CellInput.ScriptPattern(pattern));
    }
}
