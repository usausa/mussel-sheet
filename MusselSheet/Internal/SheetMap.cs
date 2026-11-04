namespace MusselSheet.Internal;

using System.Globalization;

// Covers the whole sheet, including cells that virtualization does not render, so JS never has to ask C# on a
// move or keystroke. Identical rules are stored once (Rules), and so are identical per-row sequences (Patterns).
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
    IReadOnlyDictionary<string, string> Messages,
    IReadOnlyList<int> Merges,
    CalendarInfo Calendar,
    ReadingInfo Reading,
    IReadOnlyList<EraInfo> Eras);

internal sealed record ReadingInfo(string Decimal, string Group, string Order, string EditDate, bool Japanese);

internal sealed record EraInfo(string Name, string Initial, string Start);

internal sealed record CalendarInfo(IReadOnlyList<string> DayNames, int FirstDay, string YearMonth, IReadOnlyList<string> MonthNames)
{
    public static CalendarInfo From(CultureInfo culture, DayOfWeek firstDay)
    {
        var format = culture.DateTimeFormat;
        return new CalendarInfo([.. format.AbbreviatedDayNames], (int)firstDay, format.YearMonthPattern, [.. format.MonthGenitiveNames.Take(12)]);
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
    IReadOnlyList<RuleItem>? Items,
    string? Message,
    string? Ime,
    string? Mask,
    string? Restriction,
    int Conversions,
    bool Suggest,
    bool Multiline,
    string? Note,
    string? Group,
    EditorFont? Font,
    bool Corrects);

internal sealed record EditorFont(bool Bold, bool Italic, bool Underline, bool Strikethrough, int? Size, string? Color);

internal sealed record RuleItem(string Value, string Text);

internal static class SheetMapBuilder
{
    public static SheetMap Build(Sheet sheet, int headerHeight, int rowHeaderWidth, SheetValidationMode defaultPolicy, InputContext? context = null, CalendarInfo? calendar = null)
    {
        context ??= new InputContext(sheet.Culture, DateOnly.FromDateTime(DateTime.Today), SheetTexts.English, sheet.Culture.DateTimeFormat.ShortDatePattern, ReadingRules.From(sheet.Culture, false));
        calendar ??= CalendarInfo.From(sheet.Culture, sheet.Culture.DateTimeFormat.FirstDayOfWeek);
        var rules = new List<InputRule>();
        var ruleIndex = new Dictionary<ResolvedCellSettings, int>(ReferenceEqualityComparer.Instance);
        var patterns = new List<IReadOnlyList<int>>();
        var patternIndex = new Dictionary<int[], int>(PatternComparer.Instance);
        var rowPatterns = new int[sheet.RowCount];

        // Rules are rectangles, so the rules for a row only change at a rule's top and bottom edges; the rows between
        // share one sequence. Resolving only the first row of each run keeps 10,000 rows proportional to the rule count.
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
            context.ForScript(),
            [.. sheet.MergeAreas.SelectMany(x => new[] { x.Top, x.Left, x.Bottom, x.Right })],
            calendar,
            context.Reading.ForScript(),
            [.. context.Eras.Select(x => new EraInfo(x.Name, x.Initial, x.Start.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)))]);
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
        _ => "text",
    };

    public static string? RestrictionName(CellCharacterRestriction restriction) => restriction switch
    {
        CellCharacterRestriction.Fullwidth => "fullwidth",
        CellCharacterRestriction.Halfwidth => "halfwidth",
        CellCharacterRestriction.Hiragana => "hiragana",
        CellCharacterRestriction.Katakana => "katakana",
        CellCharacterRestriction.HalfwidthKatakana => "halfwidthKatakana",
        CellCharacterRestriction.Alphanumeric => "alphanumeric",
        CellCharacterRestriction.Digits => "digits",
        _ => null,
    };

    private static InputRule CreateRule(ResolvedCellSettings settings, SheetValidationMode defaultPolicy, InputContext context) => new(
        KindName(settings.Kind),
        settings.IsInputDisabled,
        PolicyName(settings.ValidationMode ?? defaultPolicy),
        settings.Required,
        settings.Minimum?.ToString(),
        settings.Maximum?.ToString(),
        CellInput.FormatBound(settings.Minimum, settings, context.Culture, context.DateFormat, context.Eras),
        CellInput.FormatBound(settings.Maximum, settings, context.Culture, context.DateFormat, context.Eras),
        settings.MaxLength,
        settings.MaxByteLength,
        settings.Pattern,
        (settings.Kind is CellKind.List or CellKind.AutoComplete) && (settings.Items.Count > 0) ? [.. settings.Items.Select(x => new RuleItem(x.Value, x.DisplayText))] : null,
        settings.ErrorMessage,
        settings.ImeMode == ImeMode.Off ? "half" : null,
        CellInput.ActiveMask(settings),
        RestrictionName(settings.CharacterRestriction),
        (int)settings.CharacterConversions,
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
        _ => "stay",
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
