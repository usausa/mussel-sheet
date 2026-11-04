namespace Example.Client.Models;

using System.ComponentModel;
using System.Runtime.CompilerServices;

public sealed class OrderLine : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    public string? Name
    {
        get;
        set
        {
            field = value;
            OnChanged();
        }
    }

    public decimal? Quantity
    {
        get;
        set
        {
            field = value;
            OnChanged();
            OnChanged(nameof(Amount));
        }
    }

    public decimal? Price
    {
        get;
        set
        {
            field = value;
            OnChanged();
            OnChanged(nameof(Amount));
        }
    }

    public decimal? Amount => Quantity * Price;

    private void OnChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
