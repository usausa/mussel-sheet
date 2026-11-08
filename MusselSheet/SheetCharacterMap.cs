namespace MusselSheet;

using System.Buffers;

using MusselSheet.Internal;

// Characters a language reads as others. from and to are written like a character set and pair up by position:
// "a-z" to "A-Z". A single character in to takes every character of from.
public sealed class SheetCharacterMap
{
    private readonly Dictionary<string, string> characters = new(StringComparer.Ordinal);

    private readonly Dictionary<string, string>.AlternateLookup<ReadOnlySpan<char>> lookup;

    private readonly SearchValues<char> firstCharacters;

    public SheetCharacterMap(string from, string to)
    {
        var sources = Expand(from);
        var targets = Expand(to);
        if ((targets.Count != 1) && (sources.Count != targets.Count))
        {
            throw new ArgumentException($"The map '{from}' has {sources.Count} characters for {targets.Count}.", nameof(to));
        }

        for (var i = 0; i < sources.Count; i++)
        {
            characters[char.ConvertFromUtf32(sources[i])] = char.ConvertFromUtf32(targets[targets.Count == 1 ? 0 : i]);
        }

        lookup = characters.GetAlternateLookup<ReadOnlySpan<char>>();
        firstCharacters = SearchValues.Create(string.Concat(characters.Keys.Select(static x => x[0]).Distinct()));
    }

    internal IReadOnlyDictionary<string, string> Characters => characters;

    // Only a character some key starts with is looked up, and text with none of them is returned as it is
    internal string Convert(string text)
    {
        var rest = text.AsSpan();
        if (!rest.ContainsAny(firstCharacters))
        {
            return text;
        }

        var buffer = new TextBuffer(stackalloc char[TextBuffer.StackLength], text.Length * 2);
        while (true)
        {
            var at = rest.IndexOfAny(firstCharacters);
            if (at < 0)
            {
                buffer.Append(rest);
                return buffer.ToStringAndClear();
            }

            buffer.Append(rest[..at]);
            rest = rest[at..];
            var size = (rest.Length > 1) && char.IsHighSurrogate(rest[0]) ? 2 : 1;
            if (lookup.TryGetValue(rest[..size], out var value))
            {
                buffer.Append(value);
            }
            else
            {
                buffer.Append(rest[..size]);
            }

            rest = rest[size..];
        }
    }

    private static List<int> Expand(string characters)
    {
        var ranges = SheetCharacterSet.ParseRanges(characters);
        var result = new List<int>();
        for (var i = 0; i + 1 < ranges.Length; i += 2)
        {
            for (var code = ranges[i]; code <= ranges[i + 1]; code++)
            {
                result.Add(code);
            }
        }

        return result;
    }
}
