namespace MusselSheet;

public sealed record CellListItem(string Value, string? Text = null)
{
    public string DisplayText => Text ?? Value;

    public static implicit operator CellListItem(string value) => FromString(value);

    public static CellListItem FromString(string value) => new(value);
}
