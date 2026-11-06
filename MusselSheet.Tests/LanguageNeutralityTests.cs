namespace MusselSheet;

using System.Reflection;
using System.Runtime.CompilerServices;

using MusselSheet.Internal;

// The engine knows no language: only the data under Languages does, and an app's language is the same kind of data
public sealed class LanguageNeutralityTests
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
                var text = File.ReadAllText(file);
                found.AddRange(text.EnumerateRunes().Where(static x => IsLanguageLetter(x.Value)).Select(x => $"{relative}: {x}"));
            }
        }

        Assert.Empty(found);
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

    private static bool IsLanguageLetter(int code) => code switch
    {
        >= 0x0E00 and <= 0x0E7F => true,
        >= 0x3000 and <= 0x30FF => true,
        >= 0x3400 and <= 0x9FFF => true,
        >= 0xAC00 and <= 0xD7A3 => true,
        >= 0xFF00 and <= 0xFFEF => true,
        _ => false
    };
}
