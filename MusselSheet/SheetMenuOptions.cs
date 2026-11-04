namespace MusselSheet;

public sealed class SheetMenuOptions
{
    public ISet<string> Hidden { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    public IList<SheetMenuItem> Items { get; } = [];
}
