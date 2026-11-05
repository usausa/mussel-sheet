namespace MusselSheet.Internal;

using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

// Input rules must match matchWareki in musselsheet.js
internal static partial class JapaneseEraText
{
    public static bool IsEraFormat(string format)
    {
        var quote = '\0';
        for (var i = 0; i < format.Length; i++)
        {
            var c = format[i];
            if (quote != '\0')
            {
                quote = c == quote ? '\0' : quote;
            }
            else if (c is '\'' or '"')
            {
                quote = c;
            }
            else if (c == '\\')
            {
                i++;
            }
            else if (c is 'g' or 'e')
            {
                return true;
            }
        }

        return false;
    }

    // Era tokens follow Excel: ggg is the full name, gg the short name, g the initial, ee and e the era year with
    // and without padding; a single e prints 元 for era year 1 when followed by 年. Dates before the first era
    // use the culture's short date pattern.
    public static string Format(DateOnly date, string format, CultureInfo culture, IEnumerable<SheetJapaneseEra> eras)
    {
        var era = EraOf(date, eras);
        if (era is null)
        {
            return date.ToString(culture.DateTimeFormat.ShortDatePattern, culture);
        }

        var names = culture.DateTimeFormat;
        var eraYear = date.Year - era.Start.Year + 1;
        var builder = new StringBuilder();
        var i = 0;
        while (i < format.Length)
        {
            var c = format[i];
            if (c is '\'' or '"')
            {
                var end = format.IndexOf(c, i + 1);
                end = end < 0 ? format.Length : end;
                builder.Append(format, i + 1, end - i - 1);
                i = end + 1;
                continue;
            }

            if (c == '\\')
            {
                if (i + 1 < format.Length)
                {
                    builder.Append(format[i + 1]);
                }

                i += 2;
                continue;
            }

            var count = 1;
            while ((i + count < format.Length) && (format[i + count] == c))
            {
                count++;
            }

            var next = i + count < format.Length ? format[i + count] : '\0';
            builder.Append(c switch
            {
                'g' => count >= 3 ? era.Name : count == 2 ? era.ShortName : era.Initial,
                'e' when (count == 1) && (eraYear == 1) && (next == '年') => "元",
                'e' => eraYear.ToString(count >= 2 ? "00" : "0", CultureInfo.InvariantCulture),
                'y' => count >= 3 ? date.Year.ToString("0000", CultureInfo.InvariantCulture) : (date.Year % 100).ToString("00", CultureInfo.InvariantCulture),
                'M' => count >= 4 ? names.GetMonthName(date.Month) : count == 3 ? names.GetAbbreviatedMonthName(date.Month) : date.Month.ToString(count == 2 ? "00" : "0", CultureInfo.InvariantCulture),
                'd' => count >= 4 ? names.GetDayName(date.DayOfWeek) : count == 3 ? names.GetAbbreviatedDayName(date.DayOfWeek) : date.Day.ToString(count == 2 ? "00" : "0", CultureInfo.InvariantCulture),
                _ => new string(c, count)
            });
            i += count;
        }

        return builder.ToString();
    }

    // The era year may run past the era's end (Heisei 32 is 2020, same as Excel).
    // Dates before the era's first day are rejected.
    public static bool TryParse(string text, IReadOnlyList<SheetJapaneseEra> eras, out DateOnly date)
    {
        date = default;
        SheetJapaneseEra? era = null;
        foreach (var candidate in eras)
        {
            if (text.StartsWith(candidate.Name, StringComparison.Ordinal) && ((era is null) || (candidate.Name.Length > era.Name.Length)))
            {
                era = candidate;
            }
        }

        int eraYear;
        int month;
        int day;
        if (era is not null)
        {
            var match = KanjiRest().Match(text[era.Name.Length..]);
            if (!match.Success)
            {
                return false;
            }

            eraYear = match.Groups[1].Value == "元" ? 1 : Number(match.Groups[1].Value);
            month = Number(match.Groups[2].Success ? match.Groups[2].Value : match.Groups[4].Value);
            day = Number(match.Groups[3].Success ? match.Groups[3].Value : match.Groups[5].Value);
        }
        else
        {
            era = (text.Length > 0) ? eras.FirstOrDefault(x => x.Initial.Equals(text[..1], StringComparison.OrdinalIgnoreCase)) : null;
            if (era is null)
            {
                return false;
            }

            var match = InitialRest().Match(text[1..]);
            if (!match.Success)
            {
                return false;
            }

            (eraYear, month, day) = (Number(match.Groups[1].Value), Number(match.Groups[2].Value), Number(match.Groups[3].Value));
        }

        var year = era.Start.Year + eraYear - 1;
        if ((eraYear < 1) || (year > 9999) || month is < 1 or > 12 || (day < 1) || (day > DateTime.DaysInMonth(year, month)))
        {
            return false;
        }

        date = new DateOnly(year, month, day);
        return date >= era.Start;
    }

    private static SheetJapaneseEra? EraOf(DateOnly date, IEnumerable<SheetJapaneseEra> eras) =>
        eras.Where(x => x.Start <= date).MaxBy(x => x.Start);

    private static int Number(string text) => int.Parse(text, CultureInfo.InvariantCulture);

    [GeneratedRegex("^(元|[0-9]{1,2})(?:年([0-9]{1,2})月([0-9]{1,2})日|[/.-]([0-9]{1,2})[/.-]([0-9]{1,2}))$")]
    private static partial Regex KanjiRest();

    [GeneratedRegex("^([0-9]{1,2})[/.-]([0-9]{1,2})[/.-]([0-9]{1,2})$")]
    private static partial Regex InitialRest();
}
