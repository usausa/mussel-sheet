namespace MusselSheet;

using System.Globalization;

public sealed class MusselSheetOptions
{
    public MusselSheetOptions()
    {
        Cultures[string.Empty] = new SheetCulture { Texts = SheetTexts.English };
        Cultures["ja"] = new SheetCulture { Texts = SheetTexts.Japanese, JapaneseDates = true };
    }

    public IDictionary<string, SheetCulture> Cultures { get; } = new Dictionary<string, SheetCulture>(StringComparer.OrdinalIgnoreCase);

    public MusselSheetDefaults Defaults { get; } = new();

    public IDictionary<string, string?> ReadyKeys { get; } = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

    public IDictionary<string, string?> EditingKeys { get; } = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

    public SheetMenuOptions Menu { get; } = new();

    public IList<SheetJapaneseEra> Eras { get; } = [.. SheetJapaneseEra.BuiltIn];

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

    internal bool JapaneseDatesFor(CultureInfo culture) => FindValue(culture, x => x.JapaneseDates) ?? false;
}
