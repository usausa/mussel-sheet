namespace MusselSheet.Internal;

using System.Globalization;

internal static class CellText
{
    public const string CheckedText = "☑";

    public const string UncheckedText = "☐";

    private const string DefaultTimeFormat = "HH:mm";

    public static string Display(object? value, ResolvedCellSettings settings, CultureInfo culture, string? dateFormat = null, DateRules? dates = null)
    {
        if (settings.Kind == CellKind.Checkbox)
        {
            return CheckText(IsChecked(value), settings);
        }

        if (value is null)
        {
            return string.Empty;
        }

        return settings.Kind switch
        {
            CellKind.Number => FormatNumber(value, settings.Format ?? "G", culture),
            CellKind.Date => FormatDate(value, settings.Format ?? dateFormat ?? culture.DateTimeFormat.ShortDatePattern, culture, dates),
            CellKind.Time => FormatTime(value, settings.Format ?? DefaultTimeFormat, culture),
            CellKind.List or CellKind.AutoComplete => ListText(value, settings.Items, culture),
            CellKind.Custom when (settings.Format is { } format) && (value is IFormattable formattable) => formattable.ToString(format, culture),
            _ => Convert.ToString(value, culture) ?? string.Empty
        };
    }

    public static string Edit(object? value, ResolvedCellSettings settings, CultureInfo culture, DateRules? dates = null)
    {
        if (value is null)
        {
            return string.Empty;
        }

        return settings.Kind switch
        {
            CellKind.Number => FormatNumber(value, "G", culture),
            CellKind.Date => FormatDate(value, LiteralSeparators(dates?.EditFormat ?? ReadingRules.EditFormat(culture)), culture, dates),
            CellKind.Time => FormatTime(value, HasSeconds(value) ? "HH:mm:ss" : DefaultTimeFormat, culture),
            CellKind.Checkbox => IsTrue(value) ? "true" : "false",
            CellKind.List or CellKind.AutoComplete => ListText(value, settings.Items, culture),
            _ => Convert.ToString(value, culture) ?? string.Empty
        };
    }

    public static bool IsChecked(object? value) => (value is not null) && IsTrue(value);

    public static bool HasCheckText(ResolvedCellSettings settings) => (settings.CheckedText is not null) || (settings.UncheckedText is not null);

    private static string CheckText(bool on, ResolvedCellSettings settings)
    {
        if (HasCheckText(settings))
        {
            return (on ? settings.CheckedText : settings.UncheckedText) ?? string.Empty;
        }

        return on ? CheckedText : UncheckedText;
    }

    public static bool TryConvert<T>(object? value, out T? result)
    {
        result = default;
        if (value is null)
        {
            return default(T) is null;
        }

        if (value is T typed)
        {
            result = typed;
            return true;
        }

        var target = Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T);
        var converted = ConvertTo(value, target);
        if (converted is null)
        {
            return false;
        }

        result = (T)converted;
        return true;
    }

    internal static object? ConvertTo(object value, Type target)
    {
        try
        {
            return value switch
            {
                DateTime dateTime when target == typeof(DateOnly) => DateOnly.FromDateTime(dateTime),
                DateOnly date when target == typeof(DateTime) => date.ToDateTime(TimeOnly.MinValue),
                TimeSpan span when target == typeof(TimeOnly) => TimeOnly.FromTimeSpan(span),
                TimeOnly time when target == typeof(TimeSpan) => time.ToTimeSpan(),
                IConvertible convertible when typeof(IConvertible).IsAssignableFrom(target) => Convert.ChangeType(convertible, target, CultureInfo.InvariantCulture),
                _ => null
            };
        }
        catch (Exception ex) when (ex is InvalidCastException or FormatException or OverflowException or ArgumentException)
        {
            return null;
        }
    }

    private static string FormatNumber(object value, string format, CultureInfo culture) => value switch
    {
        decimal or double or float or int or long or short or byte or uint or ulong or ushort or sbyte => ((IFormattable)value).ToString(format, culture),
        _ => Convert.ToString(value, culture) ?? string.Empty
    };

    public static string FormatDate(DateOnly date, string format, CultureInfo culture, DateRules? dates) =>
        DateRules.IsEraFormat(format) ? (dates ?? DateRules.None).Format(date, format, culture) : date.ToString(format, culture);

    // The edit text is read back, so its '/' stays '/' even where the culture's date separator is something else
    private static string LiteralSeparators(string format) =>
        format.Contains('\'', StringComparison.Ordinal) ? format : format.Replace("/", "'/'", StringComparison.Ordinal);

    private static string FormatDate(object value, string format, CultureInfo culture, DateRules? dates) => value switch
    {
        DateOnly date => FormatDate(date, format, culture, dates),
        DateTime dateTime => FormatDate(DateOnly.FromDateTime(dateTime), format, culture, dates),
        DateTimeOffset offset => FormatDate(DateOnly.FromDateTime(offset.Date), format, culture, dates),
        _ => Convert.ToString(value, culture) ?? string.Empty
    };

    private static string FormatTime(object value, string format, CultureInfo culture) => value switch
    {
        TimeOnly time => time.ToString(format, culture),
        TimeSpan span when (span >= TimeSpan.Zero) && (span < TimeSpan.FromDays(1)) => TimeOnly.FromTimeSpan(span).ToString(format, culture),
        DateTime dateTime => TimeOnly.FromDateTime(dateTime).ToString(format, culture),
        _ => Convert.ToString(value, culture) ?? string.Empty
    };

    private static bool HasSeconds(object value) => value switch
    {
        TimeOnly time => time.Second != 0,
        TimeSpan span => span.Seconds != 0,
        DateTime dateTime => dateTime.Second != 0,
        _ => false
    };

    private static bool IsTrue(object value) => value switch
    {
        bool flag => flag,
        string text => string.Equals(text, "true", StringComparison.OrdinalIgnoreCase),
        _ => false
    };

    private static string ListText(object value, IEnumerable<CellListItem> items, CultureInfo culture)
    {
        var text = Convert.ToString(value, culture) ?? string.Empty;
        foreach (var item in items)
        {
            if (string.Equals(item.Value, text, StringComparison.Ordinal))
            {
                return item.DisplayText;
            }
        }

        return text;
    }
}
