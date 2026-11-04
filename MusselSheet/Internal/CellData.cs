namespace MusselSheet.Internal;

internal sealed class CellData
{
    public object? Value { get; set; }

    public string? Input { get; set; }

    public string? Error { get; set; }

    public object? Original { get; set; }

    public bool HasOriginal { get; set; }

    public bool IsEmpty => (Value is null) && (Input is null) && (Error is null) && !HasOriginal;
}
