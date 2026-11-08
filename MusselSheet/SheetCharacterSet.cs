namespace MusselSheet;

// Written as characters and ranges in one string: "0-9A-Za-z". A '-' at either end is itself.
public sealed class SheetCharacterSet
{
    private readonly int[] ranges;

    public SheetCharacterSet(string name, string characters)
    {
        Name = name;
        ranges = ParseRanges(characters);
    }

    public string Name { get; }

    public string? Message { get; init; }

    // The first and last code point of each range
    internal IReadOnlyList<int> Ranges => ranges;

    internal static int[] ParseRanges(string characters)
    {
        var codes = characters.EnumerateRunes().Select(static x => x.Value).ToArray();
        var result = new List<int>(codes.Length * 2);
        for (var i = 0; i < codes.Length; i++)
        {
            result.Add(codes[i]);
            if ((i + 2 < codes.Length) && (codes[i + 1] == '-'))
            {
                if (codes[i + 2] < codes[i])
                {
                    throw new ArgumentException($"The range '{char.ConvertFromUtf32(codes[i])}-{char.ConvertFromUtf32(codes[i + 2])}' is reversed.", nameof(characters));
                }

                i += 2;
            }

            result.Add(codes[i]);
        }

        return [.. result];
    }

    internal bool Contains(int code)
    {
        for (var i = 0; i + 1 < ranges.Length; i += 2)
        {
            if ((code >= ranges[i]) && (code <= ranges[i + 1]))
            {
                return true;
            }
        }

        return false;
    }
}
