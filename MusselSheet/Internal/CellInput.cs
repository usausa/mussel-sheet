namespace MusselSheet.Internal;

using System.Buffers;
using System.Collections.Concurrent;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

// Must stay in step with normalizeText and checkInput in MusselSheet.js: change both together
internal static partial class CellInput
{
    private const int StackLength = 256;

    // What step 2 changes or drops: full-width ASCII, the ideographic space, the minus sign and bidirectional marks
    private static readonly SearchValues<char> WidthCharacters = SearchValues.Create(
        string.Concat(Enumerable.Range(0xFF01, 0xFF5E - 0xFF01 + 1).Select(static x => (char)x)) + "\u3000\u2212\u200E\u200F\u061C");

    private static readonly TimeSpan PatternTimeout = TimeSpan.FromMilliseconds(200);

    private static readonly ConcurrentDictionary<(string Decimal, string Group), Regex> NumberPatterns = new();

    // JavaScript's whitespace and .NET's together, so both sides trim the same
    private static readonly char[] TrimCharacters =
    [
        '\t', '\n', '\v', '\f', '\r', ' ', '\u0085', '\u00A0', '\u1680', '\u2000', '\u2001', '\u2002', '\u2003', '\u2004', '\u2005',
        '\u2006', '\u2007', '\u2008', '\u2009', '\u200A', '\u2028', '\u2029', '\u202F', '\u205F', '\u3000', '\uFEFF'
    ];

    // Order: line breaks, full-width to half-width per kind, then mask shaping.
    // Text that does not fit the mask is returned unshaped and left for validation to reject.
    public static string Normalize(string text, ResolvedCellSettings settings, ReadingRules reading)
    {
        text = NormalizeLines(text, settings.WrapText);
        var mask = ActiveMask(settings);
        var normalized = mask is null ? Normalize(text, settings.Kind, reading) : NormalizeWidth(text, reading);
        return (mask is not null) && (FormatMask(normalized, mask) is { } formatted) ? formatted : normalized;
    }

    public static string NormalizeLines(string text, bool multiline)
    {
        if (!text.AsSpan().ContainsAny('\r', '\n'))
        {
            return text;
        }

        return multiline ? LineBreak().Replace(text, "\n") : LineBreak().Replace(text, string.Empty);
    }

    // What every language shares: full-width ASCII and the ideographic space become half-width, the minus sign that
    // many cultures show becomes '-', and bidirectional marks are not read. The language's own readings come next.
    public static string Normalize(string text, CellKind kind, ReadingRules reading) => kind switch
    {
        CellKind.List => text.Trim(TrimCharacters),
        CellKind.Date => reading.DateReadings?.Convert(NormalizeWidth(text, reading)) ?? NormalizeWidth(text, reading),
        CellKind.Number or CellKind.Time or CellKind.Checkbox => NormalizeWidth(text, reading),
        _ => text
    };

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

    public static bool IsAllowed(string text, SheetCharacterSet set)
    {
        foreach (var rune in text.EnumerateRunes())
        {
            if (!set.Contains(rune.Value))
            {
                return false;
            }
        }

        return true;
    }

    public static string CharacterMessage(SheetCharacterSet set, SheetTexts texts) =>
        texts.CharacterSets.GetValueOrDefault(set.Name) ?? set.Message ?? texts.Characters;

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
            case CellKind.Time when TryParseTime(text, context.Reading, out var time):
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

    public static bool OmitsYear(string text, ReadingRules reading) =>
        reading.Dates.OmitsYear(text) ?? (TrySplitDate(text, reading, out var year, out _, out _) && year.IsEmpty);

    public static string? Validate(object? value, string text, ResolvedCellSettings settings, InputContext context)
    {
        var texts = context.Texts;
        if (value is null or "")
        {
            return settings.Required ? texts.Required : null;
        }

        return CheckRange(value, settings, context) ?? CheckLength(value, settings, texts) ?? CheckCharacters(value, settings, texts) ?? CheckPattern(text, settings.Pattern, texts);
    }

    public static string? FormatBound(CellBound? bound, ResolvedCellSettings settings, CultureInfo culture, string dateFormat, DateRules? dates = null)
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
            return CellText.FormatDate(date, settings.Kind == CellKind.Date ? settings.Format ?? dateFormat : dateFormat, culture, dates);
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
            count += SheetCharacters.Halfwidth.Contains(rune.Value) ? 1 : 2;
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

    [GeneratedRegex("^(?:[0-9]{4}|[0-9]{6}|[0-9]{8})$")]
    private static partial Regex DigitsDateText();

    [GeneratedRegex("^([0-9]{1,2}):([0-9]{2})(?::([0-9]{2}))?$")]
    private static partial Regex TimeText();

    [GeneratedRegex("\r\n|\n|\r")]
    private static partial Regex LineBreak();

    // Decimal and group separators come from the sheet culture (en-US: '.' and ','; de-DE: ',' and '.').
    // At least one digit is required. When the group separator is a space (French and others), a plain space is
    // accepted too. Same as parseNumber in MusselSheet.js.
    private static bool TryParseNumber(string text, ReadingRules reading, out decimal number)
    {
        number = 0;
        if (!text.AsSpan().ContainsAnyInRange('0', '9') || !NumberPattern(reading).IsMatch(text))
        {
            return false;
        }

        var plain = text;
        if (IsBlankSeparator(reading.GroupSeparator))
        {
            plain = text.Replace(" ", string.Empty, StringComparison.Ordinal).Replace("\u00A0", string.Empty, StringComparison.Ordinal).Replace("\u202F", string.Empty, StringComparison.Ordinal);
        }
        else if (reading.GroupSeparator.Length > 0)
        {
            plain = text.Replace(reading.GroupSeparator, string.Empty, StringComparison.Ordinal);
        }

        plain = plain.Replace(reading.DecimalSeparator, ".", StringComparison.Ordinal);
        return decimal.TryParse(plain, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out number);
    }

    private static Regex NumberPattern(ReadingRules reading) =>
        NumberPatterns.GetOrAdd((reading.DecimalSeparator, reading.GroupSeparator), static key =>
        {
            var group = IsBlankSeparator(key.Group) ? "|[ \u00A0\u202F]" : key.Group.Length > 0 ? $"|{Regex.Escape(key.Group)}" : string.Empty;
            return new Regex($"^[+-]?(?:[0-9](?:[0-9]{group})*)?(?:{Regex.Escape(key.Decimal)}[0-9]+)?$", RegexOptions.CultureInvariant, PatternTimeout);
        });

    private static bool IsBlankSeparator(string separator) => (separator.Length > 0) && char.IsWhiteSpace(separator[0]);

    // The language's forms come first. Then separators are '/', '-' and '.'; the field order follows the sheet culture,
    // but a leading 4-digit year is read under any culture. A missing year uses today's year.
    // Same as matchDate in MusselSheet.js.
    private static bool TryParseDate(string text, InputContext context, out DateOnly date)
    {
        var (today, reading) = (context.Today, context.Reading);
        if (reading.Dates.TryRead(text, today, out date) is { } read)
        {
            return read;
        }

        date = default;
        if (!TrySplitDate(text, reading, out var yearText, out var monthText, out var dayText))
        {
            return false;
        }

        var year = yearText.IsEmpty ? today.Year : int.Parse(yearText, provider: CultureInfo.InvariantCulture);
        if (yearText.Length == 2)
        {
            year = FullYear(year);
        }

        var month = int.Parse(monthText, provider: CultureInfo.InvariantCulture);
        var day = int.Parse(dayText, provider: CultureInfo.InvariantCulture);
        if ((year < 1) || month is < 1 or > 12 || (day < 1) || (day > DateTime.DaysInMonth(year, month)))
        {
            return false;
        }

        date = new DateOnly(year, month, day);
        return true;
    }

    private static bool TrySplitDate(string text, ReadingRules reading, out ReadOnlySpan<char> year, out ReadOnlySpan<char> month, out ReadOnlySpan<char> day)
    {
        var order = reading.DateOrder;
        year = month = day = default;

        // Some cultures write "5. 11. 2025." and "5.11.2025."
        text = text.Replace(". ", ".", StringComparison.Ordinal).TrimEnd('.');
        if (YearFirstDateText().Match(text) is { Success: true } iso)
        {
            year = iso.Groups[1].ValueSpan;
            month = iso.Groups[2].ValueSpan;
            day = iso.Groups[3].ValueSpan;
            return true;
        }

        var full = order == DateOrder.YearMonthDay ? ShortYearFirstDateText().Match(text) : YearLastDateText().Match(text);
        if (full.Success)
        {
            var (a, b, c) = (full.Groups[1], full.Groups[2], full.Groups[3]);
            year = (order == DateOrder.YearMonthDay ? a : c).ValueSpan;
            month = (order == DateOrder.MonthDayYear ? a : b).ValueSpan;
            day = (order == DateOrder.YearMonthDay ? c : order == DateOrder.MonthDayYear ? b : a).ValueSpan;
            return true;
        }

        if (YearlessDateText().Match(text) is { Success: true } yearless)
        {
            var dayFirst = order == DateOrder.DayMonthYear;
            month = yearless.Groups[dayFirst ? 2 : 1].ValueSpan;
            day = yearless.Groups[dayFirst ? 1 : 2].ValueSpan;
            return true;
        }

        if (DigitsDateText().IsMatch(text))
        {
            // Digit-only forms by order: year-first yyyyMMdd, yyMMdd, MMdd; month-first MMddyyyy, MMddyy, MMdd;
            // day-first ddMMyyyy, ddMMyy, ddMM.
            var digits = text.AsSpan();
            var yearFirst = order == DateOrder.YearMonthDay;
            var dayFirst = order == DateOrder.DayMonthYear;
            year = yearFirst ? digits[..^4] : digits[4..];
            var back = yearFirst ? digits[^4..] : digits[..4];
            month = dayFirst ? back[2..] : back[..2];
            day = dayFirst ? back[..2] : back[2..];
            return true;
        }

        return false;
    }

    // Same pivot as Excel: 00 to 29 are 20xx, 30 to 99 are 19xx
    internal static int FullYear(int year) => year < 30 ? 2000 + year : 1900 + year;

    // The culture's separator is read as well as ':'
    private static bool TryParseTime(string text, ReadingRules reading, out TimeOnly time)
    {
        time = default;
        var match = TimeText().Match(reading.TimeSeparator is "" or ":" ? text : text.Replace(reading.TimeSeparator, ":", StringComparison.Ordinal));
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
            _ => (false, false)
        };
        if (!below && !above)
        {
            return null;
        }

        var lowText = FormatBound(minimum, settings, context.Culture, context.DateFormat, context.Dates);
        var highText = FormatBound(maximum, settings, context.Culture, context.DateFormat, context.Dates);
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
            return CharacterMessage(settings.CharacterRestriction, texts);
        }

        return (ActiveMask(settings) is { } mask) && (FormatMask(text, mask) is null) ? InputContext.Fill(texts.Mask, MaskDisplay(mask)) : null;
    }

    private static string NormalizeWidth(string text, ReadingRules reading)
    {
        var width = ToHalfwidth(text);
        return (reading.Readings?.Convert(width) ?? width).Trim(TrimCharacters);
    }

    // Text with nothing to change is returned as it is. The result is never longer than the text.
    private static string ToHalfwidth(string text)
    {
        var source = text.AsSpan();
        var first = source.IndexOfAny(WidthCharacters);
        if (first < 0)
        {
            return text;
        }

        char[]? rented = null;
        var buffer = text.Length <= StackLength ? stackalloc char[StackLength] : (rented = ArrayPool<char>.Shared.Rent(text.Length));
        source[..first].CopyTo(buffer);
        var length = first;
        foreach (var ch in source[first..])
        {
            switch (ch)
            {
                case >= '\uFF01' and <= '\uFF5E':
                    buffer[length++] = (char)(ch - 0xFEE0);
                    break;
                case '\u3000':
                    buffer[length++] = ' ';
                    break;
                case '\u2212':
                    buffer[length++] = '-';
                    break;
                case '\u200E' or '\u200F' or '\u061C':
                    break;
                default:
                    buffer[length++] = ch;
                    break;
            }
        }

        var result = new string(buffer[..length]);
        if (rented is not null)
        {
            ArrayPool<char>.Shared.Return(rented);
        }

        return result;
    }

    private static bool FitsMask(char token, char ch) => token switch
    {
        '9' => char.IsAsciiDigit(ch),
        'A' => char.IsAsciiLetter(ch),
        _ => char.IsAsciiLetterOrDigit(ch)
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
