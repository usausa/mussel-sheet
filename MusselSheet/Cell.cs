namespace MusselSheet;

using System.Diagnostics.CodeAnalysis;

[SuppressMessage("Naming", "CA1721:Property names should not match get methods", Justification = "GetValue<T> is the typed form of Value, same as Sheet.GetValue<T>")]
public sealed class Cell
{
    internal Cell(Sheet sheet, int row, int column)
    {
        Sheet = sheet;
        Address = new CellAddress(row, column);
    }

    public Sheet Sheet { get; }

    public CellAddress Address { get; }

    public object? Value
    {
        get => Sheet.GetValue(Address.Row, Address.Column);
        set => Sheet.SetValue(Address.Row, Address.Column, value);
    }

    public string? InputText => Sheet.GetInputText(Address.Row, Address.Column);

    public string Text => Sheet.GetText(Address.Row, Address.Column);

    public string EditText => Sheet.GetEditText(Address.Row, Address.Column);

    public string? Error
    {
        get => Sheet.GetError(Address.Row, Address.Column);
        set => Sheet.SetError(Address.Row, Address.Column, value);
    }

    public ResolvedCellSettings Settings => Sheet.GetSettings(Address.Row, Address.Column);

    public bool IsChanged => Sheet.IsChanged(Address.Row, Address.Column);

    public object? OriginalValue => Sheet.GetOriginalValue(Address.Row, Address.Column);

    public T? GetValue<T>() => Sheet.GetValue<T>(Address.Row, Address.Column);

    public bool TryGetValue<T>(out T? value) => Sheet.TryGetValue(Address.Row, Address.Column, out value);

    public void Configure(Action<CellSettings> configure) => Sheet.Configure(CellRange.Cell(Address), configure);

    public void ClearContents() => Sheet.ClearContents(CellRange.Cell(Address));

    public override string ToString() => Address.ToString();
}
