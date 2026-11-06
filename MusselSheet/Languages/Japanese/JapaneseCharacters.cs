namespace MusselSheet.Languages.Japanese;

public static class JapaneseCharacters
{
    public static SheetCharacterSet Hiragana { get; } = new("hiragana", "ぁ-ゖゝゞー 　") { Message = "Enter hiragana." };

    public static SheetCharacterSet Katakana { get; } = new("katakana", "ァ-ヾ 　") { Message = "Enter full-width katakana." };

    public static SheetCharacterSet HalfwidthKatakana { get; } = new("halfwidthKatakana", "･-ﾟ ") { Message = "Enter half-width katakana." };
}
