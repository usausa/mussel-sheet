namespace MusselSheet.Internal;

// Same notation as comboOf in MusselSheet.js: Ctrl+Alt+Shift+key, the key upper-cased, a space as SPACE
internal static class KeyCombo
{
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
        foreach (var modifier in parts[..^1])
        {
            if (modifier.Length == 0)
            {
                continue;
            }

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

        var name = key.ToUpperInvariant();
        return String.Concat(others)
            + (ctrl ? "Ctrl+" : string.Empty)
            + (alt ? "Alt+" : string.Empty)
            + (shift ? "Shift+" : string.Empty)
            + name;
    }
}
