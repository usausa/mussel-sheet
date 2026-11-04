namespace MusselSheet.Internal;

using System.Globalization;
using System.Text.Json;

internal static class SheetDesigner
{
    private const int DefaultRowHeight = 24;

    private const int DefaultColumnWidth = 100;

    public static SheetDesign ToDesign(Sheet sheet, SheetFunctions? functions, bool includeValues) => new()
    {
        RowCount = sheet.RowCount,
        ColumnCount = sheet.ColumnCount,
        DefaultRowHeight = sheet.DefaultRowHeight == DefaultRowHeight ? null : sheet.DefaultRowHeight,
        DefaultColumnWidth = sheet.DefaultColumnWidth == DefaultColumnWidth ? null : sheet.DefaultColumnWidth,
        Culture = sheet.Culture.Name,
        FrozenRows = sheet.FrozenRows == 0 ? null : sheet.FrozenRows,
        FrozenColumns = sheet.FrozenColumns == 0 ? null : sheet.FrozenColumns,
        Rows = NullIfEmpty(sheet.Rows.Where(x => (x.Height is not null) || (x.Header is not null)).Select(x => new SheetRowDesign { Index = x.Index, Height = x.Height, Header = x.Header })),
        Columns = NullIfEmpty(sheet.Columns.Where(x => (x.Width is not null) || (x.Header is not null)).Select(x => new SheetColumnDesign { Index = x.Index, Width = x.Width, Header = x.Header })),
        Rules = NullIfEmpty(sheet.Rules.Select(x => ToRule(x, functions))),
        Merges = NullIfEmpty(sheet.MergeAreas.Select(x => x.ToString())),
        Values = NullIfEmpty(ValuesOf(sheet, includeValues)),
        Computations = NullIfEmpty(sheet.Computations.Select(x => ToComputation(sheet, x, functions)).OfType<SheetComputationDesign>()),
    };

    public static Sheet FromDesign(SheetDesign design, SheetFunctions? functions)
    {
        ArgumentNullException.ThrowIfNull(design);
        if (design.Version != 1)
        {
            throw new NotSupportedException($"The sheet design version {design.Version} is not supported.");
        }

        var sheet = new Sheet(design.RowCount, design.ColumnCount);
        using (sheet.BeginUpdate())
        {
            if (design.DefaultRowHeight is { } height)
            {
                sheet.DefaultRowHeight = height;
            }

            if (design.DefaultColumnWidth is { } width)
            {
                sheet.DefaultColumnWidth = width;
            }

            if (design.Culture is { } culture)
            {
                sheet.Culture = CultureInfo.GetCultureInfo(culture);
            }

            sheet.FrozenRows = design.FrozenRows ?? 0;
            sheet.FrozenColumns = design.FrozenColumns ?? 0;

            foreach (var row in design.Rows ?? [])
            {
                sheet.Rows[row.Index].Height = row.Height;
                sheet.Rows[row.Index].Header = row.Header;
            }

            foreach (var column in design.Columns ?? [])
            {
                sheet.Columns[column.Index].Width = column.Width;
                sheet.Columns[column.Index].Header = column.Header;
            }

            foreach (var rule in design.Rules ?? [])
            {
                sheet.Configure(ParseRange(rule.Range), ToSettings(rule, functions));
            }

            foreach (var merge in design.Merges ?? [])
            {
                sheet.Merge(ParseRange(merge));
            }

            foreach (var value in design.Values ?? [])
            {
                var cell = CellAddress.Parse(value.Cell);
                sheet.SetValue(cell.Row, cell.Column, ToValue(value));
            }

            foreach (var computation in design.Computations ?? [])
            {
                Restore(sheet, computation, functions);
            }
        }

        return sheet;
    }

    private static CellRuleDesign ToRule(SettingsRule rule, SheetFunctions? functions)
    {
        var settings = rule.Settings;
        return new CellRuleDesign
        {
            Range = rule.Range.ToString(),
            Kind = NameOf(settings.Kind),
            ReadOnly = settings.ReadOnly,
            Format = settings.Format,
            WrapText = settings.WrapText,
            MoveGroup = settings.MoveGroup,
            Required = settings.Required,
            Minimum = settings.Minimum?.ToString(),
            Maximum = settings.Maximum?.ToString(),
            MaxLength = settings.MaxLength,
            MaxByteLength = settings.MaxByteLength,
            Pattern = settings.Pattern,
            Items = settings.Items is null ? null : [.. settings.Items.Select(x => new CellListItemDesign { Value = x.Value, Text = x.Text })],
            ErrorMessage = settings.ErrorMessage,
            InputMessage = settings.InputMessage,
            ValidationMode = NameOf(settings.ValidationMode),
            ImeMode = NameOf(settings.ImeMode),
            CharacterRestriction = NameOf(settings.CharacterRestriction),
            CharacterConversions = NameOf(settings.CharacterConversions),
            Mask = settings.Mask,
            CheckedText = settings.CheckedText,
            UncheckedText = settings.UncheckedText,
            Editor = settings.Editor,
            Validator = functions?.NameOf(settings.Validator),
            Corrector = functions?.NameOf(settings.Corrector),
            Formatter = functions?.NameOf(settings.Formatter),
            StyleProvider = functions?.NameOf(settings.StyleProvider),
            SuggestionProvider = functions?.NameOf(settings.SuggestionProvider),
            Style = ToStyle(settings.Style),
        };
    }

    private static CellStyleDesign? ToStyle(CellStyle style)
    {
        var borders = style.Borders;
        var design = new CellStyleDesign
        {
            HorizontalAlignment = NameOf(style.HorizontalAlignment),
            VerticalAlignment = NameOf(style.VerticalAlignment),
            Bold = style.Bold,
            Italic = style.Italic,
            Underline = style.Underline,
            Strikethrough = style.Strikethrough,
            FontSize = style.FontSize,
            IndentLevel = style.IndentLevel,
            FontColor = style.FontColor,
            FillColor = style.FillColor,
            CssClass = style.CssClass,
            Top = ToBorder(borders.Top),
            Right = ToBorder(borders.Right),
            Bottom = ToBorder(borders.Bottom),
            Left = ToBorder(borders.Left),
        };
        return design is
        {
            HorizontalAlignment: null, VerticalAlignment: null, Bold: null, Italic: null, Underline: null, Strikethrough: null, FontSize: null, IndentLevel: null,
            FontColor: null, FillColor: null, CssClass: null, Top: null, Right: null, Bottom: null, Left: null,
        } ? null : design;
    }

    private static CellBorderDesign? ToBorder(CellBorder? line) =>
        line is { } border ? new CellBorderDesign { Style = Camel(border.Style.ToString()), Color = border.Color } : null;

    private static IEnumerable<CellValueDesign> ValuesOf(Sheet sheet, bool includeValues)
    {
        for (var r = 0; r < sheet.RowCount; r++)
        {
            for (var c = 0; c < sheet.ColumnCount; c++)
            {
                if ((sheet.GetValue(r, c) is { } value) && (includeValues || (sheet.GetSettings(r, c).Kind == CellKind.Label)))
                {
                    yield return ToValue(new CellAddress(r, c), value);
                }
            }
        }
    }

    private static CellValueDesign ToValue(CellAddress cell, object value)
    {
        var (type, text) = value switch
        {
            string s => ("text", s),
            bool check => ("check", check ? "true" : "false"),
            DateOnly date => ("date", date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)),
            DateTime dateTime => ("date", DateOnly.FromDateTime(dateTime).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)),
            TimeOnly time => ("time", time.ToString("HH:mm:ss", CultureInfo.InvariantCulture)),
            TimeSpan span => ("time", TimeOnly.FromTimeSpan(span).ToString("HH:mm:ss", CultureInfo.InvariantCulture)),
            decimal or int or long or short or byte or sbyte or uint or ulong or ushort => ("number", Convert.ToDecimal(value, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture)),
            double or float => ("number", Convert.ToDouble(value, CultureInfo.InvariantCulture).ToString("R", CultureInfo.InvariantCulture)),
            _ => ("text", Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty),
        };
        return new CellValueDesign { Cell = cell.ToString(), Type = type, Value = text };
    }

    private static SheetComputationDesign? ToComputation(Sheet sheet, SheetComputation computation, SheetFunctions? functions)
    {
        if (functions?.NameOf(computation.Function) is not { } name)
        {
            return null;
        }

        return new SheetComputationDesign
        {
            Target = computation.CurrentTarget(),
            PerRow = computation.PerRow,
            Observed = [.. computation.Observations.Select(x => ToObserved(sheet, x))],
            Function = name,
        };
    }

    private static SheetObservedDesign ToObserved(Sheet sheet, Observation observation)
    {
        var range = observation.CurrentText(sheet);
        return new SheetObservedDesign { Name = observation.Name, Range = string.Equals(range, observation.Name, StringComparison.OrdinalIgnoreCase) ? null : range };
    }

    private static CellSettings ToSettings(CellRuleDesign rule, SheetFunctions? functions)
    {
        var settings = new CellSettings
        {
            Kind = Parse<CellKind>(rule.Kind),
            ReadOnly = rule.ReadOnly,
            Format = rule.Format,
            WrapText = rule.WrapText,
            MoveGroup = rule.MoveGroup,
            Required = rule.Required,
            Minimum = ToBound(rule.Minimum),
            Maximum = ToBound(rule.Maximum),
            MaxLength = rule.MaxLength,
            MaxByteLength = rule.MaxByteLength,
            Pattern = rule.Pattern,
            Items = rule.Items is null ? null : [.. rule.Items.Select(x => new CellListItem(x.Value, x.Text))],
            ErrorMessage = rule.ErrorMessage,
            InputMessage = rule.InputMessage,
            ValidationMode = Parse<SheetValidationMode>(rule.ValidationMode),
            ImeMode = Parse<ImeMode>(rule.ImeMode),
            CharacterRestriction = Parse<CellCharacterRestriction>(rule.CharacterRestriction),
            CharacterConversions = Parse<CellCharacterConversions>(rule.CharacterConversions),
            Mask = rule.Mask,
            CheckedText = rule.CheckedText,
            UncheckedText = rule.UncheckedText,
            Editor = rule.Editor,
            Validator = rule.Validator is { } validator ? Table(functions, validator).Validator(validator) : null,
            Corrector = rule.Corrector is { } corrector ? Table(functions, corrector).Corrector(corrector) : null,
            Formatter = rule.Formatter is { } formatter ? Table(functions, formatter).Formatter(formatter) : null,
            StyleProvider = rule.StyleProvider is { } provider ? Table(functions, provider).StyleProvider(provider) : null,
            SuggestionProvider = rule.SuggestionProvider is { } suggestions ? Table(functions, suggestions).SuggestionProvider(suggestions) : null,
        };
        ApplyStyle(settings.Style, rule.Style);
        return settings;
    }

    private static void ApplyStyle(CellStyle style, CellStyleDesign? design)
    {
        if (design is null)
        {
            return;
        }

        style.HorizontalAlignment = Parse<HorizontalAlignment>(design.HorizontalAlignment);
        style.VerticalAlignment = Parse<VerticalAlignment>(design.VerticalAlignment);
        style.Bold = design.Bold;
        style.Italic = design.Italic;
        style.Underline = design.Underline;
        style.Strikethrough = design.Strikethrough;
        style.FontSize = design.FontSize;
        style.IndentLevel = design.IndentLevel;
        style.FontColor = design.FontColor;
        style.FillColor = design.FillColor;
        style.CssClass = design.CssClass;
        style.Borders.Top = ToLine(design.Top);
        style.Borders.Right = ToLine(design.Right);
        style.Borders.Bottom = ToLine(design.Bottom);
        style.Borders.Left = ToLine(design.Left);
    }

    private static CellBorder? ToLine(CellBorderDesign? design) =>
        design is null ? null : new CellBorder(Enum.Parse<BorderLineStyle>(design.Style, true), design.Color);

    private static object? ToValue(CellValueDesign value) => value.Type.ToUpperInvariant() switch
    {
        "NUMBER" => decimal.Parse(value.Value, NumberStyles.Number | NumberStyles.AllowExponent, CultureInfo.InvariantCulture),
        "DATE" => DateOnly.ParseExact(value.Value, "yyyy-MM-dd", CultureInfo.InvariantCulture),
        "TIME" => TimeOnly.ParseExact(value.Value, "HH:mm:ss", CultureInfo.InvariantCulture),
        "CHECK" => bool.Parse(value.Value),
        _ => value.Value,
    };

    private static CellBound? ToBound(string? text)
    {
        if (text is null)
        {
            return null;
        }

        if (DateOnly.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            return date;
        }

        if (TimeOnly.TryParseExact(text, "HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out var time))
        {
            return time;
        }

        return decimal.Parse(text, NumberStyles.Number, CultureInfo.InvariantCulture);
    }

    private static void Restore(Sheet sheet, SheetComputationDesign design, SheetFunctions? functions)
    {
        var compute = Table(functions, design.Function).Computation(design.Function);
        var observations = design.Observed.Select(x =>
        {
            var range = x.Range ?? x.Name;
            return design.PerRow && CellAddress.TryParseColumn(range, out _) ? Observation.SameRow(sheet, range, x.Name) : Observation.Of(sheet, range, x.Name);
        }).ToArray();
        sheet.Restore(ParseRange(design.Target), design.PerRow, observations, compute);
    }

    private static SheetFunctions Table(SheetFunctions? functions, string name) =>
        functions ?? throw new InvalidOperationException($"The design uses the function '{name}'. Pass the SheetFunctions that has it.");

    private static CellRange ParseRange(string text) => text == "*" ? CellRange.All : CellRange.Parse(text);

    private static string? NameOf<T>(T? value)
        where T : struct, Enum =>
        value is { } item ? Camel(item.ToString()) : null;

    private static string Camel(string text) => string.Join(", ", text.Split(", ").Select(JsonNamingPolicy.CamelCase.ConvertName));

    private static T? Parse<T>(string? text)
        where T : struct, Enum =>
        text is null ? null : Enum.Parse<T>(text, true);

    private static List<T>? NullIfEmpty<T>(IEnumerable<T> items)
    {
        var list = items.ToList();
        return list.Count == 0 ? null : list;
    }
}
