namespace MusselSheet.Internal;

using System.Collections.Concurrent;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

// Must stay in step with normalizeText and checkInput in musselsheet.js: change both together
internal static partial class CellInput
{
    // Full-width counterparts, indexed by the half-width code point minus U+FF61
    private const string FullwidthKana = "。「」、・ヲァィゥェォャュョッーアイウエオカキクケコサシスセソタチツテトナニヌネノハヒフヘホマミムメモヤユヨラリルレロワン゛゜";

    // Kana that become the next code point with a dakuten; the ha row is two ahead with a handakuten
    private const string VoicedKana = "カキクケコサシスセソタチツテトハヒフヘホ";

    private const string SemiVoicedKana = "ハヒフヘホ";

    private static readonly TimeSpan PatternTimeout = TimeSpan.FromMilliseconds(200);

    private static readonly ConcurrentDictionary<(string Decimal, string Group), Regex> NumberPatterns = new();

    // Order: line breaks, full-width to half-width per kind, automatic conversions, then mask shaping.
    // Text that does not fit the mask is returned unshaped and left for validation to reject.
    public static string Normalize(string text, ResolvedCellSettings settings, ReadingRules reading)
    {
        text = NormalizeLines(text, settings.WrapText);
        var mask = ActiveMask(settings);
        var normalized = mask is null ? Normalize(text, settings.Kind, reading) : NormalizeWidth(text);
        normalized = ConvertCharacters(normalized, settings.CharacterConversions);
        return (mask is not null) && (FormatMask(normalized, mask) is { } formatted) ? formatted : normalized;
    }

    public static string NormalizeLines(string text, bool multiline) =>
        multiline ? LineBreak().Replace(text, "\n") : LineBreak().Replace(text, string.Empty);

    // Full-width alphanumerics and symbols become half-width and the text is trimmed. The long-vowel mark and minus
    // sign become '-' (typing '-5' under a Japanese IME yields 'ー5'). In Japanese reading, '・' in a date becomes
    // '/', because typing '/' under a Japanese IME yields '・'.
    public static string Normalize(string text, CellKind kind, ReadingRules reading) => kind switch
    {
        CellKind.List => text.Trim(),
        CellKind.Date when reading.Japanese => NormalizeWidth(text).Replace('・', '/'),
        CellKind.Date => NormalizeWidth(text),
        CellKind.Number or CellKind.Time or CellKind.Checkbox => NormalizeWidth(text),
        _ => text,
    };

    public static string ConvertCharacters(string text, CellCharacterConversions conversions)
    {
        if ((conversions & CellCharacterConversions.FullwidthAsciiToHalfwidth) != 0)
        {
            text = ToHalfwidthAscii(text);
        }

        if ((conversions & CellCharacterConversions.HalfwidthKatakanaToFullwidth) != 0)
        {
            text = ToFullwidthKatakana(text);
        }

        if ((conversions & CellCharacterConversions.HiraganaToKatakana) != 0)
        {
            text = ShiftKana(text, 'ぁ', 'ゖ', 'ゝ', 0x60);
        }

        if ((conversions & CellCharacterConversions.KatakanaToHiragana) != 0)
        {
            text = ShiftKana(text, 'ァ', 'ヶ', 'ヽ', -0x60);
        }

        return text;
    }

    public static string? ActiveMask(ResolvedCellSettings settings) =>
        (settings.Kind is CellKind.Text or CellKind.Mask) && !string.IsNullOrEmpty(settings.Mask) ? settings.Mask : null;

    // Separators in the mask may be left out of the input or typed as another separator character
    public static string? FormatMask(string text, string mask)
    {
        var builder = new StringBuilder(mask.Length);
        var i = 0;
        for (var m = 0; m < mask.Length; m++)
        {
            var token = mask[m];
            var escaped = (token == '\\') && (m + 1 < mask.Length);
            if (escaped)
            {
                token = mask[++m];
            }

            if (!escaped && token is '9' or 'A' or 'X')
            {
                if ((i >= text.Length) || !FitsMask(token, text[i]))
                {
                    return null;
                }

                builder.Append(text[i++]);
                continue;
            }

            if ((i < text.Length) && ((text[i] == token) || (!char.IsAsciiLetterOrDigit(token) && IsSeparator(text[i]))))
            {
                i++;
            }

            builder.Append(token);
        }

        return i == text.Length ? builder.ToString() : null;
    }

    public static string MaskDisplay(string mask)
    {
        var builder = new StringBuilder(mask.Length);
        for (var m = 0; m < mask.Length; m++)
        {
            if ((mask[m] == '\\') && (m + 1 < mask.Length))
            {
                m++;
            }

            builder.Append(mask[m]);
        }

        return builder.ToString();
    }

    public static bool IsAllowed(string text, CellCharacterRestriction restriction)
    {
        if (restriction == CellCharacterRestriction.None)
        {
            return true;
        }

        foreach (var rune in text.EnumerateRunes())
        {
            if (!IsAllowed(rune.Value, restriction))
            {
                return false;
            }
        }

        return true;
    }

    // A corrected string value is validated by the corrected text, not the typed one
    public static string? Check(string text, ResolvedCellSettings settings, InputContext context, out object? value, out bool converted, Func<object?, object?>? correct = null)
    {
        converted = TryConvert(text, settings, context, out value, out var error);
        if (converted && (correct is not null))
        {
            value = correct(value);
            text = value as string ?? text;
        }

        error ??= Validate(value, text, settings, context);
        return error is null ? null : settings.ErrorMessage ?? error;
    }

    public static bool TryConvert(string text, ResolvedCellSettings settings, InputContext context, out object? value, out string? error)
    {
        value = null;
        error = null;
        if (text.Length == 0)
        {
            return true;
        }

        switch (settings.Kind)
        {
            case CellKind.Number when TryParseNumber(text, context.Reading, out var number):
                value = number;
                return true;
            case CellKind.Number:
                error = context.Texts.Number;
                return false;
            case CellKind.Date when TryParseDate(text, context, out var date):
                value = date;
                return true;
            case CellKind.Date:
                error = context.DateMessage;
                return false;
            case CellKind.Time when TryParseTime(text, out var time):
                value = time;
                return true;
            case CellKind.Time:
                error = context.Texts.Time;
                return false;
            case CellKind.Checkbox when bool.TryParse(text, out var flag):
                value = flag;
                return true;
            case CellKind.Checkbox:
                error = context.Texts.Check;
                return false;
            case CellKind.List:
                return TryMatchItem(text, settings.Items, context.Texts, out value, out error);
            default:
                value = text;
                return true;
        }
    }

    public static bool OmitsYear(string text, ReadingRules reading) => TrySplitDate(text, reading, out var year, out _, out _) && (year is null);

    public static string? Validate(object? value, string text, ResolvedCellSettings settings, InputContext context)
    {
        var texts = context.Texts;
        if (value is null or "")
        {
            return settings.Required ? texts.Required : null;
        }

        return CheckRange(value, settings, context) ?? CheckLength(value, settings, texts) ?? CheckCharacters(value, settings, texts) ?? CheckPattern(text, settings.Pattern, texts);
    }

    public static string? FormatBound(CellBound? bound, ResolvedCellSettings settings, CultureInfo culture, string dateFormat, IReadOnlyList<SheetJapaneseEra>? eras = null)
    {
        if (bound is not { } value)
        {
            return null;
        }

        if (value.Number is { } number)
        {
            return number.ToString(settings.Kind == CellKind.Number ? settings.Format ?? "G" : "G", culture);
        }

        if (value.Date is { } date)
        {
            return CellText.FormatDate(date, settings.Kind == CellKind.Date ? settings.Format ?? dateFormat : dateFormat, culture, eras);
        }

        return value.Time?.ToString(settings.Kind == CellKind.Time ? settings.Format ?? "HH:mm" : "HH:mm", culture);
    }

    public static int CountCharacters(string text)
    {
        var count = 0;
        foreach (var unused in text.EnumerateRunes())
        {
            count++;
        }

        return count;
    }

    public static int CountBytes(string text)
    {
        var count = 0;
        foreach (var rune in text.EnumerateRunes())
        {
            count += ((rune.Value <= 0x7F) || (rune.Value is >= 0xFF61 and <= 0xFF9F)) ? 1 : 2;
        }

        return count;
    }

    [GeneratedRegex("^([0-9]{4})[/.-]([0-9]{1,2})[/.-]([0-9]{1,2})$")]
    private static partial Regex YearFirstDateText();

    [GeneratedRegex("^([0-9]{2})[/.-]([0-9]{1,2})[/.-]([0-9]{1,2})$")]
    private static partial Regex ShortYearFirstDateText();

    [GeneratedRegex("^([0-9]{1,2})[/.-]([0-9]{1,2})[/.-]([0-9]{2}|[0-9]{4})$")]
    private static partial Regex YearLastDateText();

    [GeneratedRegex("^([0-9]{1,2})[/.-]([0-9]{1,2})$")]
    private static partial Regex YearlessDateText();

    [GeneratedRegex("^(?:([0-9]{4}|[0-9]{2})年)?([0-9]{1,2})月([0-9]{1,2})日$")]
    private static partial Regex KanjiDateText();

    [GeneratedRegex("^(?:[0-9]{4}|[0-9]{6}|[0-9]{8})$")]
    private static partial Regex DigitsDateText();

    [GeneratedRegex("^([0-9]{1,2}):([0-9]{2})(?::([0-9]{2}))?$")]
    private static partial Regex TimeText();

    [GeneratedRegex("\r\n|\n|\r")]
    private static partial Regex LineBreak();

    // Decimal and group separators come from the sheet culture (en-US: '.' and ','; de-DE: ',' and '.').
    // At least one digit is required. When the group separator is a space (French and others), a plain space is
    // accepted too. Same as parseNumber in musselsheet.js.
    private static bool TryParseNumber(string text, ReadingRules reading, out decimal number)
    {
        number = 0;
        if (!text.Any(char.IsAsciiDigit) || !NumberPattern(reading).IsMatch(text))
        {
            return false;
        }

        var plain = IsBlankSeparator(reading.GroupSeparator)
            ? text.Replace(" ", string.Empty, StringComparison.Ordinal).Replace("\u00A0", string.Empty, StringComparison.Ordinal).Replace("\u202F", string.Empty, StringComparison.Ordinal)
            : text.Replace(reading.GroupSeparator, string.Empty, StringComparison.Ordinal);
        plain = plain.Replace(reading.DecimalSeparator, ".", StringComparison.Ordinal);
        return decimal.TryParse(plain, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out number);
    }

    private static Regex NumberPattern(ReadingRules reading) =>
        NumberPatterns.GetOrAdd((reading.DecimalSeparator, reading.GroupSeparator), static key =>
        {
            var group = IsBlankSeparator(key.Group) ? "[ \u00A0\u202F]" : Regex.Escape(key.Group);
            return new Regex($"^[+-]?(?:[0-9](?:[0-9]|{group})*)?(?:{Regex.Escape(key.Decimal)}[0-9]+)?$", RegexOptions.CultureInvariant, PatternTimeout);
        });

    private static bool IsBlankSeparator(string separator) => (separator.Length > 0) && char.IsWhiteSpace(separator[0]);

    // Separators are '/', '-' and '.'; the field order follows the sheet culture, but a leading 4-digit year is read
    // under any culture. A missing year uses today's year, and Japanese reading also accepts kanji forms and wareki.
    // Same as matchDate in musselsheet.js.
    private static bool TryParseDate(string text, InputContext context, out DateOnly date)
    {
        var (today, reading) = (context.Today, context.Reading);
        if (reading.Japanese && JapaneseEraText.TryParse(text, context.Eras, out date))
        {
            return true;
        }

        date = default;
        if (!TrySplitDate(text, reading, out var yearText, out var monthText, out var dayText))
        {
            return false;
        }

        var year = yearText is null ? today.Year : int.Parse(yearText, CultureInfo.InvariantCulture);
        if (yearText?.Length == 2)
        {
            year = FullYear(year);
        }

        var month = int.Parse(monthText, CultureInfo.InvariantCulture);
        var day = int.Parse(dayText, CultureInfo.InvariantCulture);
        if ((year < 1) || month is < 1 or > 12 || (day < 1) || (day > DateTime.DaysInMonth(year, month)))
        {
            return false;
        }

        date = new DateOnly(year, month, day);
        return true;
    }

    private static bool TrySplitDate(string text, ReadingRules reading, out string? year, out string month, out string day)
    {
        var order = reading.DateOrder;
        year = null;
        month = day = string.Empty;
        if (YearFirstDateText().Match(text) is { Success: true } iso)
        {
            (year, month, day) = (iso.Groups[1].Value, iso.Groups[2].Value, iso.Groups[3].Value);
            return true;
        }

        var full = order == DateOrder.YearMonthDay ? ShortYearFirstDateText().Match(text) : YearLastDateText().Match(text);
        if (full.Success)
        {
            var (a, b, c) = (full.Groups[1].Value, full.Groups[2].Value, full.Groups[3].Value);
            (year, month, day) = order switch
            {
                DateOrder.YearMonthDay => (a, b, c),
                DateOrder.MonthDayYear => (c, a, b),
                _ => (c, b, a),
            };
            return true;
        }

        if (YearlessDateText().Match(text) is { Success: true } yearless)
        {
            var (a, b) = (yearless.Groups[1].Value, yearless.Groups[2].Value);
            (month, day) = order == DateOrder.DayMonthYear ? (b, a) : (a, b);
            return true;
        }

        if (DigitsDateText().IsMatch(text))
        {
            // Digit-only forms by order: year-first yyyyMMdd, yyMMdd, MMdd; month-first MMddyyyy, MMddyy, MMdd;
            // day-first ddMMyyyy, ddMMyy, ddMM.
            var (front, back) = order == DateOrder.YearMonthDay ? (text[..^4], text[^4..]) : (text[4..], text[..4]);
            year = front.Length == 0 ? null : front;
            (month, day) = order == DateOrder.DayMonthYear ? (back[2..], back[..2]) : (back[..2], back[2..]);
            return true;
        }

        if (reading.Japanese && (KanjiDateText().Match(text) is { Success: true } kanji))
        {
            year = kanji.Groups[1].Success ? kanji.Groups[1].Value : null;
            (month, day) = (kanji.Groups[2].Value, kanji.Groups[3].Value);
            return true;
        }

        return false;
    }

    // Same pivot as Excel: 00 to 29 are 20xx, 30 to 99 are 19xx
    private static int FullYear(int year) => year < 30 ? 2000 + year : 1900 + year;

    private static bool TryParseTime(string text, out TimeOnly time)
    {
        time = default;
        var match = TimeText().Match(text);
        if (!match.Success)
        {
            return false;
        }

        var hour = int.Parse(match.Groups[1].ValueSpan, CultureInfo.InvariantCulture);
        var minute = int.Parse(match.Groups[2].ValueSpan, CultureInfo.InvariantCulture);
        var second = match.Groups[3].Success ? int.Parse(match.Groups[3].ValueSpan, CultureInfo.InvariantCulture) : 0;
        if ((hour > 23) || (minute > 59) || (second > 59))
        {
            return false;
        }

        time = new TimeOnly(hour, minute, second);
        return true;
    }

    private static bool TryMatchItem(string text, IReadOnlyList<CellListItem> items, SheetTexts texts, out object? value, out string? error)
    {
        value = null;
        error = null;
        if (items.Count == 0)
        {
            value = text;
            return true;
        }

        foreach (var item in items)
        {
            if (string.Equals(item.Value, text, StringComparison.OrdinalIgnoreCase) || string.Equals(item.DisplayText, text, StringComparison.OrdinalIgnoreCase))
            {
                value = item.Value;
                return true;
            }
        }

        error = texts.List;
        return false;
    }

    private static string? CheckRange(object value, ResolvedCellSettings settings, InputContext context)
    {
        var minimum = settings.Minimum;
        var maximum = settings.Maximum;
        var (below, above) = value switch
        {
            decimal number => ((minimum?.Number is { } low) && (number < low), (maximum?.Number is { } high) && (number > high)),
            DateOnly date => ((minimum?.Date is { } low) && (date < low), (maximum?.Date is { } high) && (date > high)),
            TimeOnly time => ((minimum?.Time is { } low) && (time < low), (maximum?.Time is { } high) && (time > high)),
            _ => (false, false),
        };
        if (!below && !above)
        {
            return null;
        }

        var lowText = FormatBound(minimum, settings, context.Culture, context.DateFormat, context.Eras);
        var highText = FormatBound(maximum, settings, context.Culture, context.DateFormat, context.Eras);
        if ((lowText is not null) && (highText is not null))
        {
            return InputContext.Fill(context.Texts.Between, lowText, highText);
        }

        return lowText is not null ? InputContext.Fill(context.Texts.AtLeast, lowText) : InputContext.Fill(context.Texts.AtMost, highText ?? string.Empty);
    }

    private static string? CheckLength(object value, ResolvedCellSettings settings, SheetTexts texts)
    {
        if (value is not string text)
        {
            return null;
        }

        if ((settings.MaxLength is { } maxLength) && (CountCharacters(text) > maxLength))
        {
            return InputContext.Fill(texts.MaxLength, maxLength);
        }

        if ((settings.MaxByteLength is { } maxByteLength) && (CountBytes(text) > maxByteLength))
        {
            return InputContext.Fill(texts.MaxByteLength, maxByteLength);
        }

        return null;
    }

    private static string? CheckCharacters(object value, ResolvedCellSettings settings, SheetTexts texts)
    {
        if ((value is not string text) || (settings.Kind is not (CellKind.Text or CellKind.Mask or CellKind.AutoComplete)))
        {
            return null;
        }

        if (!IsAllowed(text, settings.CharacterRestriction))
        {
            return RestrictionMessage(settings.CharacterRestriction, texts);
        }

        return (ActiveMask(settings) is { } mask) && (FormatMask(text, mask) is null) ? InputContext.Fill(texts.Mask, MaskDisplay(mask)) : null;
    }

    private static string RestrictionMessage(CellCharacterRestriction restriction, SheetTexts texts) => restriction switch
    {
        CellCharacterRestriction.Fullwidth => texts.Fullwidth,
        CellCharacterRestriction.Halfwidth => texts.Halfwidth,
        CellCharacterRestriction.Hiragana => texts.Hiragana,
        CellCharacterRestriction.Katakana => texts.Katakana,
        CellCharacterRestriction.HalfwidthKatakana => texts.HalfwidthKatakana,
        CellCharacterRestriction.Alphanumeric => texts.Alphanumeric,
        _ => texts.Digits,
    };

    private static bool IsHalfwidth(int code) => (code <= 0x7F) || code is >= 0xFF61 and <= 0xFF9F;

    private static bool IsAllowed(int code, CellCharacterRestriction restriction) => restriction switch
    {
        CellCharacterRestriction.Fullwidth => !IsHalfwidth(code),
        CellCharacterRestriction.Halfwidth => IsHalfwidth(code),
        CellCharacterRestriction.Hiragana => code is (>= 0x3041 and <= 0x3096) or 0x309D or 0x309E or 0x30FC or 0x20 or 0x3000,
        CellCharacterRestriction.Katakana => code is (>= 0x30A1 and <= 0x30FE) or 0x20 or 0x3000,
        CellCharacterRestriction.HalfwidthKatakana => code is (>= 0xFF65 and <= 0xFF9F) or 0x20,
        CellCharacterRestriction.Alphanumeric => code is (>= '0' and <= '9') or (>= 'A' and <= 'Z') or (>= 'a' and <= 'z'),
        CellCharacterRestriction.Digits => code is >= '0' and <= '9',
        _ => true,
    };

    private static string NormalizeWidth(string text)
    {
        var builder = new StringBuilder(text.Length);
        foreach (var ch in text)
        {
            builder.Append(ch switch
            {
                >= '！' and <= '～' => (char)(ch - 0xFEE0),
                '　' => ' ',
                'ー' or '−' or '‐' or '―' => '-',
                _ => ch,
            });
        }

        return builder.ToString().Trim();
    }

    private static string ToHalfwidthAscii(string text)
    {
        var builder = new StringBuilder(text.Length);
        foreach (var ch in text)
        {
            builder.Append(ch switch
            {
                >= '！' and <= '～' => (char)(ch - 0xFEE0),
                '　' => ' ',
                _ => ch,
            });
        }

        return builder.ToString();
    }

    private static string ToFullwidthKatakana(string text)
    {
        var builder = new StringBuilder(text.Length);
        for (var i = 0; i < text.Length; i++)
        {
            var ch = text[i];
            if (ch is < '｡' or > 'ﾟ')
            {
                builder.Append(ch);
                continue;
            }

            var kana = FullwidthKana[ch - 0xFF61];
            var next = i + 1 < text.Length ? text[i + 1] : '\0';
            if ((next == 'ﾞ') && (Voice(kana) is { } voiced))
            {
                builder.Append(voiced);
                i++;
            }
            else if ((next == 'ﾟ') && SemiVoicedKana.Contains(kana, StringComparison.Ordinal))
            {
                builder.Append((char)(kana + 2));
                i++;
            }
            else
            {
                builder.Append(kana);
            }
        }

        return builder.ToString();
    }

    private static char? Voice(char kana) => kana switch
    {
        'ウ' => 'ヴ',
        'ワ' => 'ヷ',
        'ヲ' => 'ヺ',
        _ when VoicedKana.Contains(kana, StringComparison.Ordinal) => (char)(kana + 1),
        _ => null,
    };

    private static string ShiftKana(string text, char first, char last, char iteration, int shift)
    {
        var builder = new StringBuilder(text.Length);
        foreach (var ch in text)
        {
            var inRange = ((ch >= first) && (ch <= last)) || (ch == iteration) || (ch == iteration + 1);
            builder.Append(inRange ? (char)(ch + shift) : ch);
        }

        return builder.ToString();
    }

    private static bool FitsMask(char token, char ch) => token switch
    {
        '9' => char.IsAsciiDigit(ch),
        'A' => char.IsAsciiLetter(ch),
        _ => char.IsAsciiLetterOrDigit(ch),
    };

    private static bool IsSeparator(char ch) => (ch <= 0x7F) && !char.IsAsciiLetterOrDigit(ch);

    // The pattern must match the whole text; an invalid pattern is not validated, same as in JS
    private static string? CheckPattern(string text, string? pattern, SheetTexts texts)
    {
        if (string.IsNullOrEmpty(pattern))
        {
            return null;
        }

        try
        {
            return Regex.IsMatch(text, $"^(?:{pattern})$", RegexOptions.None, PatternTimeout) ? null : texts.Pattern;
        }
        catch (RegexMatchTimeoutException)
        {
            return texts.Pattern;
        }
        catch (ArgumentException)
        {
            return null;
        }
    }
}
