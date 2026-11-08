namespace Example.Client.Models;

using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Runtime.CompilerServices;

public sealed class Order : INotifyPropertyChanged
{
    private readonly List<OrderLine> watched = [];

    public Order()
    {
        Lines.CollectionChanged += OnLinesChanged;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public DateOnly? OrderDate
    {
        get;
        set
        {
            field = value;
            OnChanged();
        }
    }

    public string? Customer
    {
        get;
        set
        {
            field = value;
            OnChanged();
        }
    }

    public string? Department
    {
        get;
        set
        {
            field = value;
            OnChanged();
        }
    }

    public ObservableCollection<OrderLine> Lines { get; } = [];

    public int LineCount => Lines.Count;

    public decimal Total => Lines.Sum(static x => x.Amount ?? 0m);

    private void OnLinesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        foreach (var line in watched)
        {
            line.PropertyChanged -= OnLineChanged;
        }

        watched.Clear();
        watched.AddRange(Lines);
        foreach (var line in watched)
        {
            line.PropertyChanged += OnLineChanged;
        }

        OnChanged(nameof(LineCount));
        OnChanged(nameof(Total));
    }

    private void OnLineChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (string.IsNullOrEmpty(e.PropertyName) || (e.PropertyName == nameof(OrderLine.Amount)))
        {
            OnChanged(nameof(Total));
        }
    }

    private void OnChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
