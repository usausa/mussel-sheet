namespace MusselSheet;

public sealed record CellSuggestionContext(Sheet Sheet, CellAddress Address, string Text, CancellationToken CancellationToken);
