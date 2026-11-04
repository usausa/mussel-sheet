namespace MusselSheet.Internal;

internal sealed record CommitResult(bool Ok, string? Error, bool Stored);

internal sealed record CustomEditorResult(bool Closed, string? Error);

internal sealed record PasteResult(int Top, int Left, int Bottom, int Right, string? Message);

internal sealed record ClipboardText(string Text, string Html);
