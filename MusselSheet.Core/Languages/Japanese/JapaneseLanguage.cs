namespace MusselSheet.Languages.Japanese;

// The model for a language: it uses only the public API, so a language in any assembly works the same
public static class JapaneseLanguage
{
    public static SheetCulture CreateCulture() => new()
    {
        Texts = JapaneseTexts.Default,
        Eras =
        {
            new SheetEra("明治", "明", "M", new DateOnly(1868, 9, 8)),
            new SheetEra("大正", "大", "T", new DateOnly(1912, 7, 30)),
            new SheetEra("昭和", "昭", "S", new DateOnly(1926, 12, 25)),
            new SheetEra("平成", "平", "H", new DateOnly(1989, 1, 8)),
            new SheetEra("令和", "令", "R", new DateOnly(2019, 5, 1))
        },
        EraFirstYear = new SheetEraFirstYear("元", "年"),
        DateForms =
        {
            "ggge年M月d日",
            "gge年M月d日",
            "ggge/M/d",
            "ge/M/d",
            "yyyy年M月d日",
            "yy年M月d日",
            "M月d日"
        },

        // A Japanese IME types the hyphen key as the long-vowel mark, and '/' as the middle dot
        Readings = new SheetCharacterMap("ー‐―", "-"),
        DateReadings = new SheetCharacterMap("・", "/")
    };
}
