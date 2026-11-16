namespace MusselSheet.Languages;

using MusselSheet.Languages.Japanese;

// The only place the library names its languages; they use only the public API
internal static class BuiltInLanguages
{
    private static readonly SheetCharacterSet[] Sets =
    [
        SheetCharacters.None,
        SheetCharacters.Fullwidth,
        SheetCharacters.Halfwidth,
        SheetCharacters.Alphanumeric,
        SheetCharacters.Digits,
        JapaneseCharacters.Hiragana,
        JapaneseCharacters.Katakana,
        JapaneseCharacters.HalfwidthKatakana
    ];

    public static void AddTo(MusselSheetOptions options)
    {
        options.Cultures[string.Empty] = new SheetCulture { Texts = SheetTexts.English };
        options.Cultures["ja"] = JapaneseLanguage.CreateCulture();
    }

    public static SheetCharacterSet? FindSet(string name)
    {
        foreach (var set in Sets)
        {
            if (set.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                return set;
            }
        }

        return null;
    }
}
