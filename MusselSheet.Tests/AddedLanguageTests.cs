namespace MusselSheet;

using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

using MusselSheet.Internal;

using Smart.Text.Japanese;

// A language other than Japanese, written with the same public API, needs nothing else
public sealed class AddedLanguageTests : BunitContext
{
    private static readonly DateOnly Today = new(2026, 11, 6);

    private static readonly MusselSheetOptions Options = TestLanguages.Options();

    public AddedLanguageTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Theory]
    [InlineData("zh-TW", "民國114年11月5日", 2025, 11, 5)]
    [InlineData("zh-TW", "民國元年1月1日", 1912, 1, 1)]
    [InlineData("zh-TW", "114年11月5日", 2025, 11, 5)]
    [InlineData("zh-TW", "2025年11月5日", 2025, 11, 5)]
    [InlineData("zh-TW", "2025/11/5", 2025, 11, 5)]
    [InlineData("th-TH", "5/11/2568", 2025, 11, 5)]
    [InlineData("th-TH", "๕/๑๑/๒๕๖๘", 2025, 11, 5)]
    [InlineData("th-TH", "5/11/2568 พ.ศ.", 2025, 11, 5)]
    [InlineData("th-TH", "พ.ศ. 2568/11/5", 2025, 11, 5)]
    [InlineData("ko-KR", "2025년 11월 5일", 2025, 11, 5)]
    [InlineData("ko-KR", "2025년11월5일", 2025, 11, 5)]
    [InlineData("ko-KR", "11월 5일", 2026, 11, 5)]
    public void TheLanguageReadsItsOwnDates(string culture, string text, int year, int month, int day)
    {
        Assert.Equal(new DateOnly(year, month, day), ReadDate(culture, text));
    }

    [Theory]
    [InlineData("zh-TW", "令和7年11月5日")]
    [InlineData("zh-TW", "民國0年1月1日")]
    [InlineData("th-TH", "令和7年11月5日")]
    [InlineData("ko-KR", "2025年11月5日")]
    public void ALanguageDoesNotReadAnotherOnes(string culture, string text)
    {
        Assert.Null(ReadDate(culture, text));
    }

    [Theory]
    [InlineData("zh-TW", "ggge年M月d日", 2025, 11, 5, "民國114年11月5日")]
    [InlineData("zh-TW", "ggge年M月d日", 1912, 1, 1, "民國元年1月1日")]
    [InlineData("zh-TW", "ge/M/d", 2025, 11, 5, "114/11/5")]
    [InlineData("th-TH", "d/M/e", 2025, 11, 5, "5/11/2568")]
    [InlineData("th-TH", "ggg e", 2025, 11, 5, "พ.ศ. 2568")]
    [InlineData("en-US", "ggge", 2025, 11, 5, "11/5/2025")]
    public void TheLanguageShowsItsOwnEras(string culture, string format, int year, int month, int day, string expected)
    {
        var info = ReadingRules.Gregorian(TestLanguages.Culture(culture));

        Assert.Equal(expected, CellText.FormatDate(new DateOnly(year, month, day), format, info, Options.DatesFor(info)));
    }

    [Fact]
    public async Task TheSheetShowsEditsAndKeepsItsOwnYears()
    {
        Services.AddMusselSheet(static o => o.Cultures["th"] = TestLanguages.Thai());
        var sheet = new Sheet(1, 1) { Culture = TestLanguages.Culture("th-TH") };
        sheet.Range("A1").Configure(static c => c.Kind = CellKind.Date);
        var cut = Render<MusselSheetView>(p => p.Add(x => x.Sheet, sheet));

        await cut.InvokeAsync(() => cut.Instance.CommitAsync(0, 0, "๕/๑๑/๒๕๖๘", false));

        Assert.Equal(new DateOnly(2025, 11, 5), sheet.GetValue(0, 0));
        Assert.Equal("5/11/2568", cut.Find(".mussel-sheet-row[data-r='0'] .mussel-sheet-cell[data-c='0']").TextContent);

        sheet.SetValue(0, 0, new DateOnly(2030, 1, 2));
        var text = cut.Instance.GetEditText(0, 0);

        Assert.Equal("2/1/2573", text);
        Assert.Equal("2/1/2573", sheet["A1"].Text);
        Assert.Equal("2/1/2573", sheet["A1"].EditText);
        await cut.InvokeAsync(() => cut.Instance.CommitAsync(0, 0, text, false));
        Assert.Equal(new DateOnly(2030, 1, 2), sheet.GetValue(0, 0));
    }

    [Fact]
    public async Task ACharacterSetOfAnotherLanguageWorksInACell()
    {
        var sheet = new Sheet(1, 2);
        sheet.Range("A1").Configure(static c => c.CharacterRestriction = TestLanguages.Hangul);
        sheet.Range("B1").Configure(static c => c.CharacterRestriction = TestLanguages.Cyrillic);
        var cut = Render<MusselSheetView>(p => p.Add(x => x.Sheet, sheet));

        Assert.Equal("한글로 입력하세요.", (await cut.InvokeAsync(() => cut.Instance.CommitAsync(0, 0, "abc", false))).Error);
        Assert.Null((await cut.InvokeAsync(() => cut.Instance.CommitAsync(0, 0, "가나다 라", false))).Error);
        Assert.Equal("Enter Cyrillic letters.", (await cut.InvokeAsync(() => cut.Instance.CommitAsync(0, 1, "abc", false))).Error);
        Assert.Null((await cut.InvokeAsync(() => cut.Instance.CommitAsync(0, 1, "Привет мир", false))).Error);
    }

    // The browser checks only the type of a cell with a Corrector, so a rule that fails after it fails only in C#
    [Fact]
    public async Task AnAppCanConvertInACorrector()
    {
        var sheet = new Sheet(1, 1);
        sheet.Range("A1").Configure(static c =>
        {
            c.Corrector = static x => x.Value is string text ? KanaConverter.Convert(text, KanaOption.HiraganaToKatakana | KanaOption.HankanaToKatakana) : x.Value;
            c.CharacterRestriction = JapaneseCharacters.Katakana;
        });
        var cut = Render<MusselSheetView>(p => p.Add(x => x.Sheet, sheet));

        Assert.Null((await cut.InvokeAsync(() => cut.Instance.CommitAsync(0, 0, "やまだ ﾀﾛｳ", true))).Error);
        Assert.Equal("ヤマダ タロウ", sheet.GetValue(0, 0));
        Assert.Equal("ヤマダ タロウ", cut.Instance.GetEditText(0, 0));

        await cut.InvokeAsync(() => cut.Instance.CommitAsync(0, 0, "山田", true));

        Assert.Equal("山田", sheet.GetValue(0, 0));
        Assert.Equal(JapaneseTexts.Default.CharacterSets["katakana"], sheet.GetError(0, 0));
    }

    [Fact]
    public void TheBrowserGetsOnlyWhatTheSheetUses()
    {
        var plain = SheetMapBuilder.Build(new Sheet(1, 1), 24, 40, SheetValidationMode.Stay);
        Assert.Empty(plain.Reading.Sets);

        var sheet = new Sheet(1, 1);
        sheet.Range("A1").Configure(static c => c.CharacterRestriction = TestLanguages.Hangul);
        var map = SheetMapBuilder.Build(sheet, 24, 40, SheetValidationMode.Stay);

        Assert.Equal(["hangul"], map.Reading.Sets.Keys);
        Assert.Equal("한글로 입력하세요.", map.Rules[0].RestrictionMessage);
    }

    [Fact]
    public void ADesignKeepsAddedSetsByName()
    {
        var functions = new SheetFunctions();
        functions.AddCharacterSet(TestLanguages.Hangul);
        var sheet = new Sheet(1, 1);
        sheet.Range("A1").Configure(static c => c.CharacterRestriction = TestLanguages.Hangul);

        var json = SheetDesignJson.Write(sheet.ToDesign());
        var copy = Sheet.FromDesign(SheetDesignJson.Read(json), functions);

        Assert.Contains("\"hangul\"", json, StringComparison.Ordinal);
        Assert.Same(TestLanguages.Hangul, copy.GetSettings(0, 0).CharacterRestriction);
        Assert.Throws<InvalidOperationException>(() => Sheet.FromDesign(SheetDesignJson.Read(json)));
        Assert.Throws<KeyNotFoundException>(() => Sheet.FromDesign(SheetDesignJson.Read(json), new SheetFunctions()));
    }

    // The browser tests read this file, so C# and the browser are tried on the same data. To write it again, run the
    // tests with MUSSELSHEET_UPDATE_FIXTURES=1.
    [Fact]
    public void TheBrowserFixtureIsWhatTheOptionsProduce()
    {
        var sets = new[] { SheetCharacters.Fullwidth, SheetCharacters.Halfwidth, SheetCharacters.Alphanumeric, SheetCharacters.Digits, JapaneseCharacters.Hiragana, JapaneseCharacters.Katakana, JapaneseCharacters.HalfwidthKatakana };
        var root = new JsonObject();
        foreach (var name in new[] { "ja-JP", "zh-TW", "th-TH", "ko-KR", "ar-SA" })
        {
            var culture = ReadingRules.Gregorian(TestLanguages.Culture(name));
            var info = Options.ReadingFor(culture).ForScript() with
            {
                Sets = sets.ToDictionary(static x => x.Name, static x => x.Ranges, StringComparer.Ordinal)
            };
            root[name] = JsonSerializer.SerializeToNode(info, SheetScriptJsonContext.Default.ReadingInfo);
        }

        var json = root.ToJsonString(new JsonSerializerOptions { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }).Replace("\r\n", "\n", StringComparison.Ordinal) + "\n";
        var path = Path.Combine(TestPaths.Root, "MusselSheet.Tests", "js", "languages.json");
        if (Environment.GetEnvironmentVariable("MUSSELSHEET_UPDATE_FIXTURES") == "1")
        {
            File.WriteAllText(path, json.Replace("\n", "\r\n", StringComparison.Ordinal));
        }

        Assert.Equal(json, File.ReadAllText(path).Replace("\r\n", "\n", StringComparison.Ordinal));
    }

    private static DateOnly? ReadDate(string culture, string text)
    {
        var info = ReadingRules.Gregorian(TestLanguages.Culture(culture));
        var reading = Options.ReadingFor(info);
        var context = new InputContext(info, Today, SheetTexts.English, info.DateTimeFormat.ShortDatePattern, reading);
        var settings = new ResolvedCellSettings { Kind = CellKind.Date };
        return CellInput.TryConvert(CellInput.Normalize(text, settings, reading), settings, context, out var value, out _) ? value as DateOnly? : null;
    }
}
