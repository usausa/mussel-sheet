namespace MusselSheet.Internal;

using System.Globalization;

// Covers the whole sheet, rendered or not, so JS never asks C# on a move; equal rules and rows are stored once
internal sealed record SheetMap(
    int Rows,
    int Columns,
    int HeaderHeight,
    int RowHeaderWidth,
    IReadOnlyList<int> ColumnWidths,
    int DefaultRowHeight,
    IReadOnlyList<int> CustomRowHeights,
    IReadOnlyList<InputRule> Rules,
    IReadOnlyList<IReadOnlyList<int>> Patterns,
    IReadOnlyList<int> RowPatterns,
    IReadOnlyList<int> Merges);

// What depends on the culture, the options and the UI culture, sent apart from the map and only when it changes
internal sealed record ScriptLanguage(IReadOnlyDictionary<string, string> Messages, CalendarInfo Calendar, ReadingInfo Reading);

// A language reaches the browser only as this data
internal sealed record ReadingInfo(
    string Decimal,
    string Group,
    string Time,
    string Order,
    string EditDate,
    IReadOnlyList<string> Forms,
    IReadOnlyDictionary<string, string>? Readings,
    IReadOnlyDictionary<string, string>? DateReadings,
    IReadOnlyList<EraInfo> Eras,
    string? EraFirstYear,
    string? EraFirstYearUnit);

internal sealed record EraInfo(string Name, string ShortName, string Initial, string Start, int FirstYear);

internal sealed record CalendarInfo(
    IReadOnlyList<string> DayNames,
    int FirstDay,
    string YearMonth,
    IReadOnlyList<string> MonthNames,
    IReadOnlyList<string> AbbreviatedMonthNames,
    string Era)
{
    public static CalendarInfo From(CultureInfo culture, DayOfWeek firstDay)
    {
        var format = culture.DateTimeFormat;
        return new CalendarInfo(
            [.. format.AbbreviatedDayNames],
            (int)firstDay,
            format.YearMonthPattern,
            [.. format.MonthNames.Take(12)],
            [.. format.AbbreviatedMonthNames.Take(12)],
            format.GetEraName(1));
    }
}

internal sealed record InputRule(
    string Kind,
    bool Disabled,
    string Policy,
    bool Required,
    string? Minimum,
    string? Maximum,
    string? MinimumText,
    string? MaximumText,
    int? MaxLength,
    int? MaxByteLength,
    string? Pattern,
    IReadOnlyList<ScriptItem>? Items,
    string? Message,
    string? Ime,
    string? Mask,
    IReadOnlyList<int>? Restriction,
    string? RestrictionMessage,
    bool Suggest,
    bool Multiline,
    string? Note,
    string? Group,
    EditorFont? Font,
    bool Corrects);

internal sealed record EditorFont(bool Bold, bool Italic, bool Underline, bool Strikethrough, int? Size, string? Color);

internal sealed record ScriptItem(string Value, string Text);

internal static class SheetMapBuilder
{
    public static ScriptLanguage Language(InputContext context, CalendarInfo calendar) => new(context.ForScript(), calendar, context.Reading.ForScript());

    public static SheetMap Build(Sheet sheet, int headerHeight, int rowHeaderWidth, SheetValidationMode defaultPolicy, InputContext? context = null)
    {
        context ??= new InputContext(sheet.Culture, DateOnly.FromDateTime(DateTime.Today), SheetTexts.English, sheet.Culture.DateTimeFormat.ShortDatePattern, ReadingRules.From(sheet.Culture));
        var rules = new List<InputRule>();
        var ruleIndex = new Dictionary<ResolvedCellSettings, int>(ReferenceEqualityComparer.Instance);
        var patterns = new List<IReadOnlyList<int>>();
        var patternIndex = new Dictionary<int[], int>(PatternComparer.Instance);
        var rowPatterns = new int[sheet.RowCount];

        // Rules change only at their top and bottom edges, so only the first row of each run is resolved
        var bounds = RowBounds(sheet);
        for (var i = 0; i + 1 < bounds.Count; i++)
        {
            var (start, end) = (bounds[i], bounds[i + 1]);
            var pattern = new int[sheet.ColumnCount];
            for (var c = 0; c < pattern.Length; c++)
            {
                var settings = sheet.GetSettings(start, c);
                if (!ruleIndex.TryGetValue(settings, out var index))
                {
                    index = rules.Count;
                    rules.Add(CreateRule(settings, defaultPolicy, context));
                    ruleIndex.Add(settings, index);
                }

                pattern[c] = index;
            }

            if (!patternIndex.TryGetValue(pattern, out var patternNumber))
            {
                patternNumber = patterns.Count;
                patterns.Add(pattern);
                patternIndex.Add(pattern, patternNumber);
            }

            Array.Fill(rowPatterns, patternNumber, start, end - start);
        }

        var customHeights = new List<int>();
        foreach (var row in sheet.Rows)
        {
            if (row.ActualHeight != sheet.DefaultRowHeight)
            {
                customHeights.Add(row.Index);
                customHeights.Add(row.ActualHeight);
            }
        }

        return new SheetMap(
            sheet.RowCount,
            sheet.ColumnCount,
            headerHeight,
            rowHeaderWidth,
            [.. sheet.Columns.Select(x => x.ActualWidth)],
            sheet.DefaultRowHeight,
            customHeights,
            rules,
            patterns,
            rowPatterns,
            [.. sheet.MergeAreas.SelectMany(x => new[] { x.Top, x.Left, x.Bottom, x.Right })]);
    }

    internal static List<int> RowBounds(Sheet sheet)
    {
        var rows = sheet.RowCount;
        var bounds = new SortedSet<int> { 0, rows };
        foreach (var rule in sheet.Rules)
        {
            var range = rule.Range;
            if ((range.Top > 0) && (range.Top < rows))
            {
                bounds.Add(range.Top);
            }

            if ((range.Bottom != CellRange.End) && (range.Bottom + 1 < rows))
            {
                bounds.Add(range.Bottom + 1);
            }
        }

        return [.. bounds];
    }

    public static string KindName(CellKind kind) => kind switch
    {
        CellKind.Label => "label",
        CellKind.Number => "number",
        CellKind.Date => "date",
        CellKind.Time => "time",
        CellKind.Checkbox => "check",
        CellKind.List => "list",
        CellKind.Mask => "mask",
        CellKind.AutoComplete => "autocomplete",
        CellKind.Custom => "custom",
        _ => "text"
    };

    private static InputRule CreateRule(ResolvedCellSettings settings, SheetValidationMode defaultPolicy, InputContext context) => new(
        KindName(settings.Kind),
        settings.IsInputDisabled,
        PolicyName(settings.ValidationMode ?? defaultPolicy),
        settings.Required,
        settings.Minimum?.ToString(),
        settings.Maximum?.ToString(),
        CellInput.FormatBound(settings.Minimum, settings, context.Culture, context.DateFormat, context.Dates),
        CellInput.FormatBound(settings.Maximum, settings, context.Culture, context.DateFormat, context.Dates),
        settings.MaxLength,
        settings.MaxByteLength,
        CellInput.ScriptPattern(settings.Pattern),
        (settings.Kind is CellKind.List or CellKind.AutoComplete) && (settings.Items.Count > 0) ? [.. settings.Items.Select(x => new ScriptItem(x.Value, x.DisplayText))] : null,
        settings.ErrorMessage,
        settings.ImeMode == ImeMode.Off ? "half" : null,
        CellInput.ActiveMask(settings),
        settings.CharacterRestriction == SheetCharacters.None ? null : settings.CharacterRestriction.Ranges,
        settings.CharacterRestriction == SheetCharacters.None ? null : CellInput.CharacterMessage(settings.CharacterRestriction, context.Texts),
        (settings.Kind == CellKind.AutoComplete) && (settings.SuggestionProvider is not null),
        settings.WrapText,
        settings.InputMessage,
        settings.MoveGroup,
        FontOf(settings.Style),
        settings.Corrector is not null);

    private static EditorFont? FontOf(ResolvedCellStyle style) =>
        style is { Bold: false, Italic: false, Underline: false, Strikethrough: false, FontSize: null, FontColor: null }
            ? null
            : new EditorFont(style.Bold, style.Italic, style.Underline, style.Strikethrough, style.FontSize, style.FontColor);

    private static string PolicyName(SheetValidationMode policy) => policy switch
    {
        SheetValidationMode.Revert => "revert",
        SheetValidationMode.AcceptAndMark => "accept",
        _ => "stay"
    };

    private sealed class PatternComparer : IEqualityComparer<int[]>
    {
        public static PatternComparer Instance { get; } = new();

        public bool Equals(int[]? x, int[]? y) => ReferenceEquals(x, y) || ((x is not null) && (y is not null) && x.AsSpan().SequenceEqual(y));

        public int GetHashCode(int[] obj)
        {
            var hash = default(HashCode);
            foreach (var value in obj)
            {
                hash.Add(value);
            }

            return hash.ToHashCode();
        }
    }
}
