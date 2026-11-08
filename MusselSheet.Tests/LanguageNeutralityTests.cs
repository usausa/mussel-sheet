namespace MusselSheet;

using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;

using MusselSheet.Internal;

// The engine knows no language: only the data under Languages does, and an app's language is the same kind of data
public sealed partial class LanguageNeutralityTests
{
    [Fact]
    public void OnlyTheLanguagesFolderHoldsALanguage()
    {
        var folder = Path.Combine(TestPaths.Root, "MusselSheet");
        var found = new List<string>();
        foreach (var file in Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(folder, file);
            if (relative.StartsWith("bin", StringComparison.Ordinal) || relative.StartsWith("obj", StringComparison.Ordinal) || relative.StartsWith("Languages", StringComparison.Ordinal))
            {
                continue;
            }

            if (Path.GetExtension(file) is ".cs" or ".razor" or ".js" or ".css" or ".csproj")
            {
                var text = Escape().Replace(File.ReadAllText(file), static x => ((char)Convert.ToInt32(x.Groups[1].Value, 16)).ToString());
                found.AddRange(text.EnumerateRunes().Where(static x => IsLanguageLetter(x)).Select(x => $"{relative}: {x} U+{x.Value:X4}"));
            }
        }

        Assert.Empty(found);
    }

    [Fact]
    public void TheScriptKnowsTheDigitsDotNetKnows()
    {
        var script = File.ReadAllText(Path.Combine(TestPaths.Root, "MusselSheet", "wwwroot", "MusselSheet.js"));
        var list = DigitZeros().Match(script).Groups[1].Value;
        var zeros = list.Split(',', StringSplitOptions.TrimEntries).Select(static x => Convert.ToInt32(x, 16));
        var expected = Enumerable.Range(0x80, 0xFF01 - 0x80).Where(static x => char.IsDigit((char)x) && (CharUnicodeInfo.GetDecimalDigitValue((char)x) == 0));

        Assert.Equal(expected, zeros);
    }

    [Theory]
    [InlineData("en-US", CellKind.Number, "۱۲۳", "123")]
    [InlineData("fa-IR", CellKind.Number, "۱۲۳", "123")]
    [InlineData("hi-IN", CellKind.Number, "१२३", "123")]
    [InlineData("en-US", CellKind.Date, "٢٠٢٥/١١/٠٥", "2025-11-05")]
    [InlineData("th-TH", CellKind.Time, "๑๒:๓๐", "12:30")]
    public void TheDigitsOfEveryScriptAreRead(string name, CellKind kind, string text, string expected)
    {
        var culture = ReadingRules.Gregorian(CultureInfo.GetCultureInfo(name));
        var reading = new MusselSheetOptions().ReadingFor(culture);
        var context = new InputContext(culture, new DateOnly(2026, 11, 8), SheetTexts.English, reading.DateFormat, reading);
        var settings = new ResolvedCellSettings { Kind = kind };

        Assert.Null(CellInput.Check(CellInput.Normalize(text, settings, reading), settings, context, out var value, out _));
        Assert.Equal(expected, value switch
        {
            DateOnly date => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            TimeOnly time => time.ToString("HH:mm", CultureInfo.InvariantCulture),
            _ => Convert.ToString(value, CultureInfo.InvariantCulture)
        });
    }

    [Fact]
    public void AListMatchesItsItemsWhateverTheWidth()
    {
        var culture = CultureInfo.GetCultureInfo("ja-JP");
        var reading = new MusselSheetOptions().ReadingFor(culture);
        var context = new InputContext(culture, new DateOnly(2026, 11, 8), SheetTexts.English, reading.DateFormat, reading);
        var settings = new ResolvedCellSettings { Kind = CellKind.List, Items = [new CellListItem("Sales", "Sales"), new CellListItem("Ｂ２", "Ｂ２")] };

        Assert.Null(CellInput.Check("Ｓａｌｅｓ", settings, context, out var sales, out _));
        Assert.Null(CellInput.Check("b2", settings, context, out var b2, out _));
        Assert.Equal("Sales", sales);
        Assert.Equal("Ｂ２", b2);
    }

    [Fact]
    public void OnlyTheTestsSeeTheInternals()
    {
        var friends = typeof(Sheet).Assembly.GetCustomAttributes<InternalsVisibleToAttribute>().Select(static x => x.AssemblyName);

        Assert.Equal(["MusselSheet.Tests"], friends);
    }

    [Fact]
    public void EnglishAndJapaneseAreBuiltIn()
    {
        var options = new MusselSheetOptions();

        Assert.Equal([string.Empty, "ja"], options.Cultures.Keys);
        Assert.Same(SheetTexts.English, options.TextsFor(TestLanguages.Culture("fr-FR")));
        Assert.Same(JapaneseTexts.Default, options.TextsFor(TestLanguages.Culture("ja-JP")));
        Assert.Empty(options.DatesFor(TestLanguages.Culture("en-US")).Eras);
        Assert.NotEmpty(options.DatesFor(TestLanguages.Culture("ja-JP")).Eras);
    }

    [Fact]
    public void AnAppCanReplaceOrRemoveABuiltInLanguage()
    {
        var culture = TestLanguages.Culture("ja-JP");
        var removed = new MusselSheetOptions();
        removed.Cultures.Remove("ja");
        var replaced = new MusselSheetOptions { Cultures = { ["ja"] = new SheetCulture { DateForms = { "yyyy-M-d'x'" } } } };

        Assert.Empty(removed.DatesFor(culture).Eras);
        Assert.Same(SheetTexts.English, removed.TextsFor(culture));
        Assert.Empty(replaced.DatesFor(culture).Eras);
        Assert.Single(replaced.DatesFor(culture).Patterns);
    }

    [Theory]
    [InlineData("１２３", "123")]
    [InlineData("－５", "-5")]
    [InlineData("−５", "-5")]
    [InlineData("\u200E-5", "-5")]
    [InlineData("\u061C-5", "-5")]
    [InlineData("ー５", "ー5")]
    [InlineData("‐５", "‐5")]
    [InlineData("―５", "―5")]
    public void WhatEveryLanguageSharesComesOnlyFromUnicodeAndTheCulture(string text, string expected)
    {
        var reading = new MusselSheetOptions().ReadingFor(TestLanguages.Culture("en-US"));

        Assert.Equal(expected, CellInput.Normalize(text, CellKind.Number, reading));
    }

    [Theory]
    [InlineData("令和7年11月5日")]
    [InlineData("R7.11.5")]
    [InlineData("11月5日")]
    [InlineData("2025年11月5日")]
    [InlineData("2025・11・5")]
    public void WithoutTheLanguageItsFormsAreNotRead(string text)
    {
        var culture = TestLanguages.Culture("en-US");
        var reading = new MusselSheetOptions().ReadingFor(culture);
        var context = new InputContext(culture, new DateOnly(2026, 11, 6), SheetTexts.English, "M/d/yyyy", reading);

        Assert.False(CellInput.TryConvert(CellInput.Normalize(text, CellKind.Date, reading), new ResolvedCellSettings { Kind = CellKind.Date }, context, out _, out _));
    }

    private static bool IsLanguageLetter(Rune rune) => rune.Value switch
    {
        0x3000 or (>= 0xFF00 and <= 0xFFEF) => false,
        >= 0x0E00 and <= 0x0E7F => true,
        >= 0x3000 and <= 0x30FF => true,
        >= 0x3400 and <= 0x9FFF => true,
        >= 0xAC00 and <= 0xD7A3 => true,
        > 0x7F => Rune.IsLetter(rune),
        _ => false
    };

    [GeneratedRegex(@"\\u([0-9A-Fa-f]{4})")]
    private static partial Regex Escape();

    [GeneratedRegex(@"NATIVE_DIGIT_ZEROS = \[([^\]]*)\]")]
    private static partial Regex DigitZeros();
}
