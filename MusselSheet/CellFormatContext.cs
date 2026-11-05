namespace MusselSheet;

public sealed record CellFormatContext(
    Sheet Sheet,
    CellAddress Address,
    object? Value,
    string Text);
