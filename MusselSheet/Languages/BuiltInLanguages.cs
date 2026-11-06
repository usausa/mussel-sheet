namespace MusselSheet.Languages;

using MusselSheet.Languages.Japanese;

// The one place the library names its languages. They use only the public API, so a language an app adds is the same.
internal static class BuiltInLanguages
{
    public static void AddTo(MusselSheetOptions options)
    {
        options.Cultures[string.Empty] = new SheetCulture { Texts = SheetTexts.English };
        options.Cultures["ja"] = JapaneseLanguage.CreateCulture();
    }

    public static SheetCharacterSet? FindSet(string name) =>
        new[]
        {
            SheetCharacters.None,
            SheetCharacters.Fullwidth,
            SheetCharacters.Halfwidth,
            SheetCharacters.Alphanumeric,
            SheetCharacters.Digits,
            JapaneseCharacters.Hiragana,
            JapaneseCharacters.Katakana,
            JapaneseCharacters.HalfwidthKatakana
        }.FirstOrDefault(x => x.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
}
