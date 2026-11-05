namespace MusselSheet;

using System.Diagnostics.CodeAnalysis;

using MusselSheet.Internal;

public sealed partial class Sheet
{
    private readonly List<ISheetBinding> bindings = [];

    public SheetBinding<T> Bind<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] T>(T source)
        where T : class
    {
        var binding = new SheetBinding<T>(this, source);
        bindings.Add(binding);
        return binding;
    }

    internal void Unbind(ISheetBinding binding) => bindings.Remove(binding);

    internal BoundProperty? FindBinding(int row, int column)
    {
        foreach (var binding in bindings)
        {
            if (binding.FindProperty(row, column) is { } property)
            {
                return property;
            }
        }

        return null;
    }

    // Removed rows and columns live on for undo with their old index, so membership is checked by identity
    internal bool Contains(SheetRow row) => (row.Index < rowList.Count) && ReferenceEquals(rowList[row.Index], row);

    internal bool Contains(SheetColumn column) => (column.Index < columnList.Count) && ReferenceEquals(columnList[column.Index], column);

    internal void InsertRowsLike(int index, int count, SheetRow source)
    {
        var after = source.Index >= index ? source.Index + count : source.Index;
        PutRows(index, CreateRows(count, source.Height), RangeTransform.Insert(settings.Rules, Axis.Row, index, count, true, after), RangeTransform.InsertMerges(merges, Axis.Row, index, count));
    }

    // Bindings are told right away, even inside BeginUpdate
    private void NotifyBindings(SheetChangedEventArgs e)
    {
        if (bindings.Count == 0)
        {
            return;
        }

        foreach (var binding in bindings.ToArray())
        {
            binding.OnSheetChanged(e);
        }
    }
}
