namespace MusselSheet;

[Flags]
public enum CellCharacterConversions
{
    None = 0,

    FullwidthAsciiToHalfwidth = 1,

    HalfwidthKatakanaToFullwidth = 2,

    HiraganaToKatakana = 4,

    KatakanaToHiragana = 8,
}
