namespace MusselSheet;

using System.Collections.Concurrent;
using System.Globalization;
using System.Runtime.CompilerServices;

using MusselSheet.Internal;
using MusselSheet.Languages;

public sealed class MusselSheetOptions
{
    private readonly ConcurrentDictionary<string, DateRules> dates = new(StringComparer.OrdinalIgnoreCase);

    private readonly ConditionalWeakTable<CultureInfo, ReadingRules> readings = [];

    public MusselSheetOptions()
    {
        BuiltInLanguages.AddTo(this);
    }

    public IDictionary<string, SheetCulture> Cultures { get; } = new Dictionary<string, SheetCulture>(StringComparer.OrdinalIgnoreCase);

    public MusselSheetDefaults Defaults { get; } = new();

    public IDictionary<string, string?> ReadyKeys { get; } = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

    public IDictionary<string, string?> EditingKeys { get; } = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

    public SheetMenuOptions Menu { get; } = new();

    internal static MusselSheetOptions Default { get; } = new();

    internal T? Find<T>(CultureInfo culture, Func<SheetCulture, T?> pick)
        where T : class
    {
        for (var current = culture; ; current = current.Parent)
        {
            if (Cultures.TryGetValue(current.Name, out var settings) && (pick(settings) is { } value))
            {
                return value;
            }

            if (current.Name.Length == 0)
            {
                return null;
            }
        }
    }

    internal T? FindValue<T>(CultureInfo culture, Func<SheetCulture, T?> pick)
        where T : struct
    {
        for (var current = culture; ; current = current.Parent)
        {
            if (Cultures.TryGetValue(current.Name, out var settings) && (pick(settings) is { } value))
            {
                return value;
            }

            if (current.Name.Length == 0)
            {
                return null;
            }
        }
    }

    internal SheetTexts TextsFor(CultureInfo uiCulture) => Find(uiCulture, x => x.Texts) ?? SheetTexts.English;

    internal string DateFormatFor(CultureInfo culture) => Find(culture, x => x.DateFormat) ?? culture.DateTimeFormat.ShortDatePattern;

    internal DayOfWeek FirstDayOfWeekFor(CultureInfo culture) => FindValue(culture, x => x.FirstDayOfWeek) ?? culture.DateTimeFormat.FirstDayOfWeek;

    // Made once per culture, because the view asks for every cell and every commit:
    // configure the options before the first sheet is shown
    internal DateRules DatesFor(CultureInfo culture) =>
        dates.TryGetValue(culture.Name, out var rules) ? rules : AddDates(culture);

    internal ReadingRules ReadingFor(CultureInfo culture)
    {
        if (!readings.TryGetValue(culture, out var reading))
        {
            reading = ReadingRules.From(culture) with
            {
                Dates = DatesFor(culture),
                EditDateFormat = Find(culture, static x => x.EditDateFormat) ?? ReadingRules.EditFormat(culture),
                Readings = Find(culture, static x => x.Readings),
                DateReadings = Find(culture, static x => x.DateReadings)
            };
            readings.AddOrUpdate(culture, reading);
        }

        return reading;
    }

    private DateRules AddDates(CultureInfo culture) => dates.GetOrAdd(culture.Name, _ => new DateRules(
        FindList(culture, static x => x.Eras),
        Find(culture, static x => x.EraFirstYear),
        FindList(culture, static x => x.DateForms),
        Find(culture, static x => x.EditDateFormat)));

    private IList<T> FindList<T>(CultureInfo culture, Func<SheetCulture, IList<T>> pick) =>
        Find(culture, x => pick(x) is { Count: > 0 } list ? list : null) ?? [];
}
