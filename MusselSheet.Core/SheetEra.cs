namespace MusselSheet;

// FirstYear is the era's own number for its start: 1 for most, 544 for one counted from long before its table
public sealed record SheetEra(string Name, string ShortName, string Initial, DateOnly Start)
{
    public int FirstYear { get; init; } = 1;
}
