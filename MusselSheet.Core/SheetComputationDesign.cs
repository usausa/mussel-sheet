namespace MusselSheet;

public sealed class SheetComputationDesign
{
    public string Target { get; set; } = string.Empty;

    public bool PerRow { get; set; }

    public IReadOnlyList<SheetObservedDesign> Observed { get; set; } = [];

    public string Function { get; set; } = string.Empty;
}
