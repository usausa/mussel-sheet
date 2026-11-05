namespace MusselSheet;

public sealed record CellDisplayContext(
    Sheet Sheet,
    CellAddress Address,
    string? Name,
    object? Value,
    string Text);
