namespace MusselSheet;

public sealed record SheetJapaneseEra(string Name, string ShortName, string Initial, DateOnly Start)
{
    public static IReadOnlyList<SheetJapaneseEra> BuiltIn { get; } =
    [
        new("明治", "明", "M", new DateOnly(1868, 9, 8)),
        new("大正", "大", "T", new DateOnly(1912, 7, 30)),
        new("昭和", "昭", "S", new DateOnly(1926, 12, 25)),
        new("平成", "平", "H", new DateOnly(1989, 1, 8)),
        new("令和", "令", "R", new DateOnly(2019, 5, 1)),
    ];
}
