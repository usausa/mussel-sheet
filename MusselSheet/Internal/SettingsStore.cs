namespace MusselSheet.Internal;

// Rules later in the list take precedence
internal sealed record SettingsRule(CellRange Range, CellSettings Settings);

internal sealed class SettingsStore
{
    private readonly List<SettingsRule> rules = [];

    private readonly Dictionary<long, ResolvedCellSettings> cache = [];

    // Equal resolved settings share one instance, which many cells use; SheetMap dedupes input rules by reference
    private readonly Dictionary<ResolvedCellSettings, ResolvedCellSettings> interned = [];

    private readonly Dictionary<long, CellEdges> edgeCache = [];

    public IReadOnlyList<SettingsRule> Rules => rules;

    public void Add(CellRange range, CellSettings settings)
    {
        rules.Add(new SettingsRule(range, settings));
        Invalidate();
    }

    public void Replace(IReadOnlyList<SettingsRule> newRules)
    {
        if (ReferenceEquals(newRules, rules))
        {
            Invalidate();
            return;
        }

        rules.Clear();
        rules.AddRange(newRules);
        Invalidate();
    }

    public ResolvedCellSettings Resolve(int row, int column)
    {
        var key = Key(row, column);
        if (cache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var builder = new SettingsBuilder();
        foreach (var rule in rules)
        {
            if (rule.Range.Contains(row, column))
            {
                builder.Apply(rule.Settings);
            }
        }

        var resolved = builder.Build();
        if (interned.TryGetValue(resolved, out var existing))
        {
            resolved = existing;
        }
        else
        {
            interned.Add(resolved, resolved);
        }

        cache[key] = resolved;
        return resolved;
    }

    // A shared edge can be set from either cell (this cell's Right or the neighbor's Left); the later rule wins.
    // hasRight and hasBelow stop the lookup at the sheet edge.
    public CellEdges ResolveEdges(int row, int column, bool hasRight, bool hasBelow)
    {
        var key = Key(row, column);
        if (edgeCache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        CellBorder? right = null;
        CellBorder? bottom = null;
        foreach (var rule in rules)
        {
            var borders = rule.Settings.Style.Borders;
            var range = rule.Range;
            if (range.Contains(row, column))
            {
                right = borders.Right ?? right;
                bottom = borders.Bottom ?? bottom;
            }

            if (hasRight && (borders.Left is { } left) && range.Contains(row, column + 1))
            {
                right = left;
            }

            if (hasBelow && (borders.Top is { } top) && range.Contains(row + 1, column))
            {
                bottom = top;
            }
        }

        var edges = new CellEdges(right, bottom);
        edgeCache[key] = edges;
        return edges;
    }

    private static long Key(int row, int column) => ((long)row << 32) | (uint)column;

    private void Invalidate()
    {
        cache.Clear();
        interned.Clear();
        edgeCache.Clear();
    }
}

internal sealed class SettingsBuilder
{
    private CellKind kind = CellKind.Text;

    private bool readOnly;

    private string? format;

    private bool multiline;

    private string? moveGroup;

    private bool required;

    private CellBound? minimum;

    private CellBound? maximum;

    private int? maxLength;

    private int? maxByteLength;

    private string? pattern;

    private IReadOnlyList<CellListItem> items = [];

    private string? errorMessage;

    private string? inputMessage;

    private Func<CellValidationContext, string?>? validator;

    private Func<CellCorrectionContext, object?>? corrector;

    private Func<CellFormatContext, string?>? formatter;

    private Func<CellFormatContext, CellStyle?>? styleProvider;

    private SheetValidationMode? validationPolicy;

    private ImeMode inputHint;

    private CellCharacterRestriction characterRestriction;

    private CellCharacterConversions characterConversions;

    private string? mask;

    private string? checkedText;

    private string? uncheckedText;

    private Func<CellSuggestionContext, Task<IReadOnlyList<string>>>? suggestionProvider;

    private string? editor;

    private HorizontalAlignment horizontalAlignment = HorizontalAlignment.General;

    private VerticalAlignment verticalAlignment = VerticalAlignment.Center;

    private bool bold;

    private bool italic;

    private bool underline;

    private bool strikethrough;

    private int? fontSize;

    private int indent;

    private string? foreground;

    private string? background;

    private string? cssClass;

    private CellBorder? borderTop;

    private CellBorder? borderRight;

    private CellBorder? borderBottom;

    private CellBorder? borderLeft;

    public void Apply(CellSettings settings)
    {
        ApplyInput(settings);
        ApplyValidation(settings);
        ApplyStyle(settings.Style);
    }

    public ResolvedCellSettings Build() => new()
    {
        Kind = kind,
        ReadOnly = readOnly,
        Format = format,
        WrapText = multiline,
        MoveGroup = string.IsNullOrEmpty(moveGroup) ? null : moveGroup,
        Required = required,
        Minimum = minimum,
        Maximum = maximum,
        MaxLength = maxLength,
        MaxByteLength = maxByteLength,
        Pattern = pattern,
        Items = items,
        ErrorMessage = errorMessage,
        InputMessage = string.IsNullOrEmpty(inputMessage) ? null : inputMessage,
        Validator = validator,
        Corrector = corrector,
        ValidationMode = validationPolicy,
        ImeMode = inputHint,
        CharacterRestriction = characterRestriction,
        CharacterConversions = characterConversions,
        Mask = mask,
        CheckedText = checkedText,
        UncheckedText = uncheckedText,
        SuggestionProvider = suggestionProvider,
        Editor = editor,
        Formatter = formatter,
        StyleProvider = styleProvider,
        Style = BuildStyle(),
    };

    private void ApplyInput(CellSettings settings)
    {
        kind = settings.Kind ?? kind;
        readOnly = settings.ReadOnly ?? readOnly;
        format = settings.Format ?? format;
        multiline = settings.WrapText ?? multiline;
        moveGroup = settings.MoveGroup ?? moveGroup;
        inputHint = settings.ImeMode ?? inputHint;
        characterRestriction = settings.CharacterRestriction ?? characterRestriction;
        characterConversions = settings.CharacterConversions ?? characterConversions;
        mask = settings.Mask ?? mask;
        checkedText = settings.CheckedText ?? checkedText;
        uncheckedText = settings.UncheckedText ?? uncheckedText;
        suggestionProvider = settings.SuggestionProvider ?? suggestionProvider;
        editor = settings.Editor ?? editor;
        formatter = settings.Formatter ?? formatter;
        styleProvider = settings.StyleProvider ?? styleProvider;
    }

    private void ApplyValidation(CellSettings settings)
    {
        required = settings.Required ?? required;
        minimum = settings.Minimum ?? minimum;
        maximum = settings.Maximum ?? maximum;
        maxLength = settings.MaxLength ?? maxLength;
        maxByteLength = settings.MaxByteLength ?? maxByteLength;
        pattern = settings.Pattern ?? pattern;
        items = settings.Items ?? items;
        errorMessage = settings.ErrorMessage ?? errorMessage;
        inputMessage = settings.InputMessage ?? inputMessage;
        validator = settings.Validator ?? validator;
        corrector = settings.Corrector ?? corrector;
        validationPolicy = settings.ValidationMode ?? validationPolicy;
    }

    private void ApplyStyle(CellStyle style)
    {
        horizontalAlignment = style.HorizontalAlignment ?? horizontalAlignment;
        verticalAlignment = style.VerticalAlignment ?? verticalAlignment;
        bold = style.Bold ?? bold;
        italic = style.Italic ?? italic;
        underline = style.Underline ?? underline;
        strikethrough = style.Strikethrough ?? strikethrough;
        fontSize = style.FontSize ?? fontSize;
        indent = style.IndentLevel ?? indent;
        foreground = style.FontColor ?? foreground;
        background = style.FillColor ?? background;
        cssClass = style.CssClass ?? cssClass;
        var borders = style.Borders;
        borderTop = borders.Top ?? borderTop;
        borderRight = borders.Right ?? borderRight;
        borderBottom = borders.Bottom ?? borderBottom;
        borderLeft = borders.Left ?? borderLeft;
    }

    private ResolvedCellStyle BuildStyle()
    {
        var borders = (borderTop is null) && (borderRight is null) && (borderBottom is null) && (borderLeft is null)
            ? ResolvedCellBorders.None
            : new ResolvedCellBorders(borderTop, borderRight, borderBottom, borderLeft);
        return new ResolvedCellStyle
        {
            HorizontalAlignment = horizontalAlignment,
            VerticalAlignment = verticalAlignment,
            Bold = bold,
            Italic = italic,
            Underline = underline,
            Strikethrough = strikethrough,
            FontSize = fontSize,
            IndentLevel = indent,
            FontColor = foreground,
            FillColor = background,
            CssClass = cssClass,
            Borders = borders,
        };
    }
}
