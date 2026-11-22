namespace Example.Client.Components.Samples;

using Microsoft.AspNetCore.Components;

public sealed class SheetEventLog
{
    private const int MaxLines = 8;

    private readonly List<string> lines = [];

    public event EventHandler? Changed;

    public IReadOnlyList<string> Lines => lines;

    public CellAddress ActiveCell { get; private set; }

    public CellRange Selection { get; private set; }

    public EventCallback<CellCommittedEventArgs> CellCommitted => EventCallback.Factory.Create<CellCommittedEventArgs>(this, OnCellCommitted);

    public EventCallback<CellAddress> ActiveCellChanged => EventCallback.Factory.Create<CellAddress>(this, OnActiveCellChanged);

    public EventCallback<CellRange> SelectionChanged => EventCallback.Factory.Create<CellRange>(this, OnSelectionChanged);

    public EventCallback<SheetMenuItemClickedEventArgs> MenuItemClicked => EventCallback.Factory.Create<SheetMenuItemClickedEventArgs>(this, OnMenuItemClicked);

    public EventCallback<T> Callback<T>(Action<T> handler) => EventCallback.Factory.Create<T>(this, handler.Invoke);

    public void OnCellCommitted(CellCommittedEventArgs e)
    {
        var error = e.Error is null ? string.Empty : $" (error: {e.Error})";
        Add($"{e.Address} {e.Reason}: {Format(e.OldValue)} → {Format(e.Value)}{error}");
    }

    public void Add(string line)
    {
        lines.Insert(0, line);
        if (lines.Count > MaxLines)
        {
            lines.RemoveAt(lines.Count - 1);
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    private static string Format(object? value) => value switch
    {
        null => "(empty)",
        DateOnly date => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        TimeOnly time => time.ToString("HH:mm", CultureInfo.InvariantCulture),
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString() ?? string.Empty
    };

    private void OnActiveCellChanged(CellAddress address)
    {
        ActiveCell = address;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void OnSelectionChanged(CellRange range)
    {
        Selection = range;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void OnMenuItemClicked(SheetMenuItemClickedEventArgs e)
    {
        Add($"Menu item {e.Name}: {e.Selection}");
    }
}
