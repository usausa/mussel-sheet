namespace MusselSheet;

public sealed class CellCommittedEventArgs : EventArgs
{
    internal CellCommittedEventArgs(Sheet sheet, CellAddress address, object? oldValue, object? value, string? error, CellChangeReason reason)
    {
        Sheet = sheet;
        Address = address;
        OldValue = oldValue;
        Value = value;
        Error = error;
        Reason = reason;
    }

    public Sheet Sheet { get; }

    public CellAddress Address { get; }

    public object? OldValue { get; }

    public object? Value { get; }

    public string? Error { get; }

    public CellChangeReason Reason { get; }
}
