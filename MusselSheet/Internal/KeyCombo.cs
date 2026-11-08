namespace MusselSheet.Internal;

// Same notation as comboOf in MusselSheet.js: Ctrl+Alt+Shift+key, a character upper-cased, a space as Space
internal static class KeyCombo
{
    private static readonly string[] Names =
    [
        "Enter", "Tab", "Escape", "Backspace", "Delete", "Insert", "Home", "End", "PageUp", "PageDown",
        "ArrowUp", "ArrowDown", "ArrowLeft", "ArrowRight", "ContextMenu", "Space",
        "F1", "F2", "F3", "F4", "F5", "F6", "F7", "F8", "F9", "F10", "F11", "F12"
    ];

    public static Dictionary<string, string?> NormalizeAll(IDictionary<string, string?> keys) =>
        keys.ToDictionary(x => Normalize(x.Key), x => x.Value, StringComparer.Ordinal);

    public static string Normalize(string combo)
    {
        var parts = combo.Split('+', StringSplitOptions.TrimEntries);
        var key = (parts[^1].Length == 0) && (parts.Length > 1) ? "+" : parts[^1];
        var modifiers = parts[..^1].Select(x => x.ToUpperInvariant()).ToHashSet();
        var name = key.Length == 1 ? key.ToUpperInvariant() : Names.FirstOrDefault(x => x.Equals(key, StringComparison.OrdinalIgnoreCase)) ?? key;
        return (modifiers.Contains("CTRL") ? "Ctrl+" : string.Empty)
            + (modifiers.Contains("ALT") ? "Alt+" : string.Empty)
            + (modifiers.Contains("SHIFT") ? "Shift+" : string.Empty)
            + name;
    }
}
