namespace MusselSheet.Internal;

internal readonly record struct CellView(
    string Class,
    string Text,
    string? Kind,
    string? ReadOnly,
    string? Ime,
    string? Edit,
    string? Error,
    string? Check,
    bool Custom,
    int? RowSpan = null,
    int? ColumnSpan = null,
    bool Covered = false,
    string? Style = null)
{
    public static CellView CoveredCell { get; } = new("mussel-sheet-cell mussel-sheet-covered", string.Empty, null, null, null, null, null, null, false, Covered: true);
}
