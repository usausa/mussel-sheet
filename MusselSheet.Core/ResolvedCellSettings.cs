namespace MusselSheet;

public sealed record ResolvedCellSettings
{
    public static ResolvedCellSettings Default { get; } = new();

    public CellKind Kind { get; init; } = CellKind.Text;

    public bool ReadOnly { get; init; }

    public string? Format { get; init; }

    public bool WrapText { get; init; }

    public string? MoveGroup { get; init; }

    public bool Required { get; init; }

    public CellBound? Minimum { get; init; }

    public CellBound? Maximum { get; init; }

    public int? MaxLength { get; init; }

    public int? MaxByteLength { get; init; }

    public string? Pattern { get; init; }

    public IReadOnlyList<CellListItem> Items { get; init; } = [];

    public string? ErrorMessage { get; init; }

    public string? InputMessage { get; init; }

    public Func<CellValidationContext, string?>? Validator { get; init; }

    public Func<CellCorrectionContext, object?>? Corrector { get; init; }

    public SheetValidationMode? ValidationMode { get; init; }

    public ImeMode ImeMode { get; init; }

    public SheetCharacterSet CharacterRestriction { get; init; } = SheetCharacters.None;

    public string? Mask { get; init; }

    public string? CheckedText { get; init; }

    public string? UncheckedText { get; init; }

    public Func<CellSuggestionContext, Task<IReadOnlyList<string>>>? SuggestionProvider { get; init; }

    public string? Editor { get; init; }

    public Func<CellFormatContext, string?>? Formatter { get; init; }

    public Func<CellFormatContext, CellStyle?>? StyleProvider { get; init; }

    public ResolvedCellStyle Style { get; init; } = ResolvedCellStyle.Default;

    public bool IsInputDisabled => ReadOnly || (Kind == CellKind.Label);
}
