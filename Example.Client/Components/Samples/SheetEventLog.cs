namespace Example.Client.Components.Samples;

// Handlers for the events of MusselSheetView. The page passes them to the view, and SampleFrame shows the result
public sealed class SheetEventLog
{
    private const int MaxLines = 8;

    private readonly List<string> lines = [];

    public IReadOnlyList<string> Lines => lines;

    public CellAddress ActiveCell { get; private set; }

    public CellRange Selection { get; private set; }

    public void OnActiveCellChanged(CellAddress address) => ActiveCell = address;

    public void OnSelectionChanged(CellRange range) => Selection = range;

    public void OnCellCommitted(CellCommittedEventArgs e)
    {
        var error = e.Error is null ? string.Empty : $" (error: {e.Error})";
        Add($"{e.Address} {e.Reason}: {Format(e.OldValue)} → {Format(e.Value)}{error}");
    }

    public void OnMenuItemClicked(SheetMenuItemClickedEventArgs e)
    {
        Add($"Menu item {e.Name}: {e.Selection}");
    }

    public void Add(string line)
    {
        lines.Insert(0, line);
        if (lines.Count > MaxLines)
        {
            lines.RemoveAt(lines.Count - 1);
        }
    }

    private static string Format(object? value) => value switch
    {
        null => "(empty)",
        DateOnly date => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        TimeOnly time => time.ToString("HH:mm", CultureInfo.InvariantCulture),
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString() ?? string.Empty
    };
}
