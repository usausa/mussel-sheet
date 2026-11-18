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
        foreach (var observation in observations)
        {
            if (String.Equals(observation.Name, observed, StringComparison.OrdinalIgnoreCase))
            {
                var values = new List<object?>();
                foreach (var cell in observation.Cells(Sheet, row))
                {
                    values.Add(Sheet.GetValue(cell.Row, cell.Column));
                }

                return values;
            }
        }

        throw new ArgumentException($"'{observed}' is not one of the observed cells.", nameof(observed));
    }
}
