namespace MusselSheet;

public sealed record CellCorrectionContext(Sheet Sheet, CellAddress Address, object? Value, string Text);
