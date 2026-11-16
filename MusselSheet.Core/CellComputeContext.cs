namespace MusselSheet;

using MusselSheet.Internal;

public sealed class CellComputeContext
{
    private readonly IReadOnlyList<Observation> observations;

    private readonly SheetRow? row;

    public Sheet Sheet { get; }

    public CellAddress Address { get; }

    internal CellComputeContext(Sheet sheet, CellAddress address, IReadOnlyList<Observation> observations, SheetRow? row)
    {
        Sheet = sheet;
        Address = address;
        this.observations = observations;
        this.row = row;
    }

    public T? Get<T>(string observed) =>
        (Values(observed) is [var first, ..]) && CellText.TryConvert<T>(first, out var value) ? value : default;

    public IReadOnlyList<object?> Values(string observed)
    {
        var observation = observations.FirstOrDefault(x => string.Equals(x.Name, observed, StringComparison.OrdinalIgnoreCase))
            ?? throw new ArgumentException($"'{observed}' is not one of the observed cells.", nameof(observed));
        return [.. observation.Cells(Sheet, row).Select(x => Sheet.GetValue(x.Row, x.Column))];
    }
}
