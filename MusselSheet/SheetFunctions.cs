namespace MusselSheet;

// Functions are saved by name: attach delegates taken from this table, because saving looks the name up from the attached delegate.
// Names share one namespace across all kinds, so keep them unique.
public sealed class SheetFunctions
{
    private readonly Dictionary<string, Delegate> functions = new(StringComparer.Ordinal);

    private readonly Dictionary<Delegate, string> names = [];

    private readonly Dictionary<string, SheetCharacterSet> characterSets = new(StringComparer.OrdinalIgnoreCase);

    public void AddValidator(string name, Func<CellValidationContext, string?> function) => Add(name, function);

    public void AddCorrector(string name, Func<CellCorrectionContext, object?> function) => Add(name, function);

    public void AddFormatter(string name, Func<CellFormatContext, string?> function) => Add(name, function);

    public void AddStyleProvider(string name, Func<CellFormatContext, CellStyle?> function) => Add(name, function);

    public void AddSuggestionProvider(string name, Func<CellSuggestionContext, Task<IReadOnlyList<string>>> function) => Add(name, function);

    public void AddComputation(string name, Func<CellComputeContext, object?> function) => Add(name, function);

    public void AddCharacterSet(SheetCharacterSet set) => characterSets[set.Name] = set;

    public Func<CellValidationContext, string?> Validator(string name) => Get<Func<CellValidationContext, string?>>(name);

    public Func<CellCorrectionContext, object?> Corrector(string name) => Get<Func<CellCorrectionContext, object?>>(name);

    public Func<CellFormatContext, string?> Formatter(string name) => Get<Func<CellFormatContext, string?>>(name);

    public Func<CellFormatContext, CellStyle?> StyleProvider(string name) => Get<Func<CellFormatContext, CellStyle?>>(name);

    public Func<CellSuggestionContext, Task<IReadOnlyList<string>>> SuggestionProvider(string name) => Get<Func<CellSuggestionContext, Task<IReadOnlyList<string>>>>(name);

    public Func<CellComputeContext, object?> Computation(string name) => Get<Func<CellComputeContext, object?>>(name);

    public SheetCharacterSet CharacterSet(string name) =>
        characterSets.TryGetValue(name, out var set) ? set : throw new KeyNotFoundException($"The character set '{name}' is not registered.");

    internal SheetCharacterSet? FindCharacterSet(string name) => characterSets.GetValueOrDefault(name);

    internal string? NameOf(Delegate? function) => (function is not null) && names.TryGetValue(function, out var name) ? name : null;

    internal T Get<T>(string name)
        where T : Delegate =>
        functions.TryGetValue(name, out var function) && (function is T typed)
            ? typed
            : throw new KeyNotFoundException($"The function '{name}' is not registered as {typeof(T).Name}.");

    private void Add(string name, Delegate function)
    {
        if (functions.Remove(name, out var old))
        {
            names.Remove(old);
        }

        functions[name] = function;
        names[function] = name;
    }
}
