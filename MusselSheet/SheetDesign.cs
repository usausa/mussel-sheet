namespace MusselSheet;

public sealed class SheetDesign
{
    public int Version { get; set; } = 1;

    public int RowCount { get; set; }

    public int ColumnCount { get; set; }

    public int? DefaultRowHeight { get; set; }

    public int? DefaultColumnWidth { get; set; }

    public string? Culture { get; set; }

    public int? FrozenRows { get; set; }

    public int? FrozenColumns { get; set; }

    public IReadOnlyList<SheetRowDesign>? Rows { get; set; }

    public IReadOnlyList<SheetColumnDesign>? Columns { get; set; }

    public IReadOnlyList<CellRuleDesign>? Rules { get; set; }

    public IReadOnlyList<string>? Merges { get; set; }

    public IReadOnlyList<CellValueDesign>? Values { get; set; }

    public IReadOnlyList<SheetComputationDesign>? Computations { get; set; }
}
