namespace MusselSheet;

using MusselSheet.Languages;

// Languages that exist only in the tests. They use the same public API as the built-in Japanese and nothing was added
// to the library for them, which is what the tests show.
internal static class TestLanguages
{
    public static SheetCharacterSet Hangul { get; } = new("hangul", "가-힣 ") { Message = "한글로 입력하세요." };

    public static SheetCharacterSet Cyrillic { get; } = new("cyrillic", "А-Яа-яЁё ") { Message = "Enter Cyrillic letters." };

    public static SheetCulture Taiwan() => new()
    {
        DateFormat = "ggge年M月d日",
        Eras = { new SheetEra("民國", "民", string.Empty, new DateOnly(1912, 1, 1)) },
        EraFirstYear = new SheetEraFirstYear("元", "年"),
        DateForms = { "gggeee年M月d日", "gggeee/M/d", "eee年M月d日", "yyyy年M月d日" }
    };

    public static SheetCulture Thai() => new()
    {
        DateFormat = "d/M/e",
        EditDateFormat = "d/M/e",
        Eras = { new SheetEra("พ.ศ.", "พ.ศ.", string.Empty, new DateOnly(1, 1, 1)) { FirstYear = 544 } },
        DateForms = { "d/M/eeee", "d/M/eeee ggg", "ggg eeee/M/d" },
        Readings = new SheetCharacterMap("๐-๙", "0-9")
    };

    public static SheetCulture Korean() => new()
    {
        Texts = new SheetTexts { Number = "숫자를 입력하세요.", CharacterSets = new Dictionary<string, string>(StringComparer.Ordinal) { ["digits"] = "숫자만 입력하세요." } },
        DateForms = { "yyyy년 M월 d일", "M월 d일" }
    };

    public static SheetCulture Arabic() => new()
    {
        Readings = new SheetCharacterMap("٠-٩۰-۹", "0-90-9")
    };

    public static MusselSheetOptions Options() => new()
    {
        Cultures =
        {
            ["zh-TW"] = Taiwan(),
            ["th"] = Thai(),
            ["ko"] = Korean(),
            ["ar"] = Arabic()
        }
    };

    // By the names they are saved under, so that attributes can name them
    public static SheetCharacterSet Set(string name) => BuiltInLanguages.FindSet(name)!;

    public static CultureInfo Culture(string name) => CultureInfo.GetCultureInfo(name);
}
