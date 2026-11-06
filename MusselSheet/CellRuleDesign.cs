namespace MusselSheet;

public sealed class CellRuleDesign
{
    public string Range { get; set; } = string.Empty;

    public string? Kind { get; set; }

    public bool? ReadOnly { get; set; }

    public string? Format { get; set; }

    public bool? WrapText { get; set; }

    public string? MoveGroup { get; set; }

    public bool? Required { get; set; }

    public string? Minimum { get; set; }

    public string? Maximum { get; set; }

    public int? MaxLength { get; set; }

    public int? MaxByteLength { get; set; }

    public string? Pattern { get; set; }

    public IReadOnlyList<CellListItemDesign>? Items { get; set; }

    public string? ErrorMessage { get; set; }

    public string? InputMessage { get; set; }

    public string? ValidationMode { get; set; }

    public string? ImeMode { get; set; }

    public string? CharacterRestriction { get; set; }

    public string? Mask { get; set; }

    public string? CheckedText { get; set; }

    public string? UncheckedText { get; set; }

    public string? Editor { get; set; }

    public string? Validator { get; set; }

    public string? Corrector { get; set; }

    public string? Formatter { get; set; }

    public string? StyleProvider { get; set; }

    public string? SuggestionProvider { get; set; }

    public CellStyleDesign? Style { get; set; }
}
