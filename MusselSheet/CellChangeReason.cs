namespace MusselSheet;

public enum CellChangeReason
{
    Input,
    ClearContents,
    Toggle,
    Undo,
    Redo,
    Paste,
    Cut,
    Fill
}
