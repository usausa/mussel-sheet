namespace MusselSheet.Internal;

using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

// Keep readForms and formatDate in MusselSheet.js in step: the browser runs the same compiled forms
internal sealed partial class DateRules
{
    private readonly Regex[] forms;

    public DateRules(IEnumerable<SheetEra> eras, SheetEraFirstYear? firstYear, IEnumerable<string> forms)
    {
        Eras = [.. eras];
        FirstYear = firstYear;
        Patterns = [.. forms.Select(Pattern).OfType<string>()];
        this.forms = [.. Patterns.Select(static x => new Regex(x, RegexOptions.CultureInvariant, CellInput.PatternTimeout))];
    }

    public static DateRules None { get; } = new([], null, []);

    public IReadOnlyList<SheetEra> Eras { get; }

    public SheetEraFirstYear? FirstYear { get; }

    public IReadOnlyList<string> Patterns { get; }

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

    // g, gg, ggg: era initial, short name, name; e, ee: era year (e before the unit writes year 1 as the first-year text)
    public string Format(DateOnly date, string format, CultureInfo culture, string? beforeEras = null)
    {
        var era = EraOf(date);
        if (era is null)
        {
            return date.ToString(beforeEras ?? culture.DateTimeFormat.ShortDatePattern, culture);
        }

        var names = culture.DateTimeFormat;
        var eraYear = date.Year - era.Start.Year + era.FirstYear;
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

            builder.Append(c switch
            {
                'g' => count >= 3 ? era.Name : count == 2 ? era.ShortName : era.Initial,
                'e' when (count == 1) && (eraYear == era.FirstYear) && (FirstYear is { } first) && format.AsSpan(i + count).StartsWith(first.Unit, StringComparison.Ordinal) => first.Text,
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

    // null: no form matched; false: a form matched but no such date (an era year may run past the era's end)
    public bool? TryRead(string text, DateOnly today, out DateOnly date) => TryRead(text, today, out date, out _);

    public bool? TryRead(string text, DateOnly today, out DateOnly date, out bool omitsYear)
    {
        date = default;
        omitsYear = false;
        if (Match(text) is not { } match)
        {
            return null;
        }

        omitsYear = !match.Groups["year"].Success && !match.Groups["eraYear"].Success;

        SheetEra? era = null;
        int year;
        if (match.Groups["eraYear"] is { Success: true } eraYearGroup)
        {
            era = FindEra(match);
            var eraYear = eraYearGroup.ValueSpan.SequenceEqual(FirstYear?.Text) ? era.FirstYear : Number(eraYearGroup.ValueSpan);
            if (eraYear < era.FirstYear)
            {
                return false;
            }

            year = era.Start.Year + eraYear - era.FirstYear;
        }
        else
        {
            var yearGroup = match.Groups["year"];
            year = !yearGroup.Success ? today.Year : yearGroup.Length == 2 ? CellInput.FullYear(Number(yearGroup.ValueSpan)) : Number(yearGroup.ValueSpan);
        }

        return CellInput.TryCreateDate(year, Number(match.Groups["month"].ValueSpan), Number(match.Groups["day"].ValueSpan), out date) && ((era is null) || (date >= era.Start));
    }

    private static int Number(ReadOnlySpan<char> text) => int.Parse(text, provider: CultureInfo.InvariantCulture);

    private static string Alternatives(IEnumerable<string> items) =>
        string.Join('|', items.Where(static x => x.Length > 0).OrderByDescending(static x => x.Length).Select(Literal));

    private static string EitherCase(string text) =>
        string.Concat(text.Select(static c => char.IsAsciiLetter(c) ? $"[{char.ToUpperInvariant(c)}{char.ToLowerInvariant(c)}]" : Literal(c.ToString())));

    private static string Literal(string text) => SpecialCharacter().Replace(text, static x => $"\\{x.Value}");

    [GeneratedRegex(@"[.*+?^${}()|\[\]\\/-]")]
    private static partial Regex SpecialCharacter();

    private Match? Match(string text)
    {
        foreach (var form in forms)
        {
            var match = form.Match(text);
            if (match.Success)
            {
                return match;
            }
        }

        return null;
    }

    private SheetEra? EraOf(DateOnly date)
    {
        SheetEra? found = null;
        foreach (var era in Eras)
        {
            if ((era.Start <= date) && ((found is null) || (era.Start > found.Start)))
            {
                found = era;
            }
        }

        return found;
    }

    private SheetEra FindEra(Match match)
    {
        var (name, shortName, initial) = (match.Groups["era"], match.Groups["short"], match.Groups["initial"]);
        foreach (var era in Eras)
        {
            if ((name.Success && name.ValueSpan.SequenceEqual(era.Name)) ||
                (shortName.Success && shortName.ValueSpan.SequenceEqual(era.ShortName)) ||
                (initial.Success && initial.ValueSpan.Equals(era.Initial, StringComparison.OrdinalIgnoreCase)))
            {
                return era;
            }
        }

        return Eras[^1];
    }

    // The display format's symbols, with '/' for any of / . - and optional spaces; null when the form cannot be read
    private string? Pattern(string form)
    {
        var builder = new StringBuilder("^");
        var fields = new HashSet<char>();
        var i = 0;
        while (i < form.Length)
        {
            var c = form[i];
            if (c is '\'' or '"')
            {
                var end = form.IndexOf(c, i + 1);
                end = end < 0 ? form.Length : end;
                builder.Append(Literal(form[(i + 1)..end]));
                i = end + 1;
                continue;
            }

            if ((c == '\\') && (i + 1 < form.Length))
            {
                builder.Append(Literal(form[(i + 1)..(i + 2)]));
                i += 2;
                continue;
            }

            var count = 1;
            while ((i + count < form.Length) && (form[i + count] == c))
            {
                count++;
            }

            i += count;
            if ((c is 'g' or 'e' or 'y' or 'M' or 'd') && !fields.Add(c))
            {
                return null;
            }

            var part = Part(c, count);
            if (part.Length == 0)
            {
                return null;
            }

            builder.Append(part);
        }

        var hasDate = fields.Contains('M') && fields.Contains('d');
        var hasYears = !fields.Contains('y') || !fields.Contains('e');
        var hasEraYear = !fields.Contains('g') || fields.Contains('e');
        return hasDate && hasYears && hasEraYear ? builder.Append('$').ToString() : null;
    }

    private string Part(char symbol, int count)
    {
        switch (symbol)
        {
            case 'g':
                var eras = count >= 3
                    ? Alternatives(Eras.Select(static x => x.Name))
                    : count == 2
                        ? Alternatives(Eras.Select(static x => x.ShortName))
                        : string.Join('|', Eras.Where(static x => x.Initial.Length > 0).Select(static x => EitherCase(x.Initial)));
                return eras.Length == 0 ? string.Empty : $"(?<{(count >= 3 ? "era" : count == 2 ? "short" : "initial")}>{eras})";
            case 'e':
                var firstYear = FirstYear is { } first ? $"{Literal(first.Text)}|" : string.Empty;
                return Eras.Count == 0 ? string.Empty : $"(?<eraYear>{firstYear}[0-9]{{1,{Math.Max(count, 2)}}})";
            case 'y':
                return count == 2 ? "(?<year>[0-9]{2})" : "(?<year>[0-9]{4})";
            case 'M':
                return count >= 3 ? string.Empty : "(?<month>[0-9]{1,2})";
            case 'd':
                return count >= 3 ? string.Empty : "(?<day>[0-9]{1,2})";
            case '/':
                return string.Concat(Enumerable.Repeat("[/.-]", count));
            case ' ':
                return " *";
            default:
                return Literal(new string(symbol, count));
        }
    }
}
