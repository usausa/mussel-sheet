namespace MusselSheet;

public sealed record CellValidationContext(
    Sheet Sheet,
    CellAddress Address,
    object? Value,
    string Text);
