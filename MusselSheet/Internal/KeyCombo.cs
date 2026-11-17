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

    public static Dictionary<string, string?> NormalizeAll(IDictionary<string, string?> keys)
    {
        var normalized = new Dictionary<string, string?>(keys.Count, StringComparer.Ordinal);
        foreach (var (key, value) in keys)
        {
            normalized[Normalize(key)] = value;
        }

        return normalized;
    }

    // Control, Meta and Cmd are Ctrl; another modifier is kept, so the key never matches
    public static string Normalize(string combo)
    {
        var parts = combo.Split('+', StringSplitOptions.TrimEntries);
        var plus = (parts[^1].Length == 0) && (parts.Length > 1);
        var key = plus ? "+" : parts[^1];
        var (ctrl, alt, shift) = (false, false, false);
        var others = new List<string>();
        foreach (var modifier in plus ? parts[..^2] : parts[..^1])
        {
            switch (modifier.ToUpperInvariant())
            {
                case "CTRL" or "CONTROL" or "META" or "CMD":
                    ctrl = true;
                    break;
                case "ALT":
                    alt = true;
                    break;
                case "SHIFT":
                    shift = true;
                    break;
                default:
                    others.Add(modifier + "+");
                    break;
            }
        }

        var name = key.Length == 1 ? key.ToUpperInvariant() : Names.FirstOrDefault(x => x.Equals(key, StringComparison.OrdinalIgnoreCase)) ?? key;
        return string.Concat(others)
            + (ctrl ? "Ctrl+" : string.Empty)
            + (alt ? "Alt+" : string.Empty)
            + (shift ? "Shift+" : string.Empty)
            + name;
    }
}
