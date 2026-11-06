namespace MusselSheet;

// FirstYear is what the era calls its starting year: 1 for most, 544 for an era counted from long before its table starts
public sealed record SheetEra(string Name, string ShortName, string Initial, DateOnly Start)
{
    public int FirstYear { get; init; } = 1;
}
