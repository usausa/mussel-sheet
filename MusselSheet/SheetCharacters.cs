namespace MusselSheet;

// The sets every language shares. Half-width is ASCII and Unicode's half-width forms.
public static class SheetCharacters
{
    public static SheetCharacterSet None { get; } = new("none", "\u0000-\U0010FFFF");

    public static SheetCharacterSet Fullwidth { get; } = new("fullwidth", "\u0080-\uFF60\uFFDD-\uFFE7\uFFEF-\U0010FFFF") { Message = "Enter full-width characters." };

    public static SheetCharacterSet Halfwidth { get; } = new("halfwidth", "\u0000-\u007F\uFF61-\uFFDC\uFFE8-\uFFEE") { Message = "Enter half-width characters." };

    public static SheetCharacterSet Alphanumeric { get; } = new("alphanumeric", "0-9A-Za-z") { Message = "Enter half-width letters and digits." };

    public static SheetCharacterSet Digits { get; } = new("digits", "0-9") { Message = "Enter half-width digits." };
}
