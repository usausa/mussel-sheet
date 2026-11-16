namespace MusselSheet;

using System.Globalization;

public sealed class SheetMenuItem
{
    public required string Name { get; init; }

    public required string Text { get; init; }

    public IDictionary<string, string> Texts { get; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    internal string TextFor(CultureInfo uiCulture)
    {
        for (var culture = uiCulture; culture.Name.Length > 0; culture = culture.Parent)
        {
            if (Texts.TryGetValue(culture.Name, out var text))
            {
                return text;
            }
        }

        return Text;
    }
}
