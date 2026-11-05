namespace MusselSheet;

public sealed class CellCommittingEventArgs : EventArgs
{
    public Sheet Sheet { get; }

    public CellAddress Address { get; }

    public string Text { get; }

    public object? Value { get; set; }

    public string? Error { get; set; }

    public CellChangeReason Reason { get; }

    public bool Cancel { get; set; }

    internal CellCommittingEventArgs(Sheet sheet, CellAddress address, string text, object? value, string? error, CellChangeReason reason)
    {
        Sheet = sheet;
        Address = address;
        Text = text;
        Value = value;
        Error = error;
        Reason = reason;
    }
}
