namespace MusselSheet;

public sealed class CellSettings
{
    public CellKind? Kind { get; set; }

    public bool? ReadOnly { get; set; }

    public string? Format { get; set; }

    public bool? WrapText { get; set; }

    public string? MoveGroup { get; set; }

    public bool? Required { get; set; }

    public CellBound? Minimum { get; set; }

    public CellBound? Maximum { get; set; }

    public int? MaxLength { get; set; }

    public int? MaxByteLength { get; set; }

    public string? Pattern { get; set; }

    public IReadOnlyList<CellListItem>? Items { get; set; }

    public string? ErrorMessage { get; set; }

    public string? InputMessage { get; set; }

    public Func<CellValidationContext, string?>? Validator { get; set; }

    public Func<CellCorrectionContext, object?>? Corrector { get; set; }

    public SheetValidationMode? ValidationMode { get; set; }

    public ImeMode? ImeMode { get; set; }

    public SheetCharacterSet? CharacterRestriction { get; set; }

    public string? Mask { get; set; }

    public string? CheckedText { get; set; }

    public string? UncheckedText { get; set; }

    public Func<CellSuggestionContext, Task<IReadOnlyList<string>>>? SuggestionProvider { get; set; }

    public string? Editor { get; set; }

    public Func<CellFormatContext, string?>? Formatter { get; set; }

    public Func<CellFormatContext, CellStyle?>? StyleProvider { get; set; }

    public CellStyle Style
    {
        get => field ??= new CellStyle();
        set;
    }

    internal CellSettings Clone() => new()
    {
        Kind = Kind,
        ReadOnly = ReadOnly,
        Format = Format,
        WrapText = WrapText,
        MoveGroup = MoveGroup,
        Required = Required,
        Minimum = Minimum,
        Maximum = Maximum,
        MaxLength = MaxLength,
        MaxByteLength = MaxByteLength,
        Pattern = Pattern,
        Items = Items,
        ErrorMessage = ErrorMessage,
        InputMessage = InputMessage,
        Validator = Validator,
        Corrector = Corrector,
        ValidationMode = ValidationMode,
        ImeMode = ImeMode,
        CharacterRestriction = CharacterRestriction,
        Mask = Mask,
        CheckedText = CheckedText,
        UncheckedText = UncheckedText,
        SuggestionProvider = SuggestionProvider,
        Editor = Editor,
        Formatter = Formatter,
        StyleProvider = StyleProvider,
        Style = Style.Clone()
    };
}
