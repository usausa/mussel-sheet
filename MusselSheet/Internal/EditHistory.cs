namespace MusselSheet.Internal;

// Records user actions only; changes made by the app are not recorded
internal sealed class EditHistory
{
    private readonly LinkedList<EditEntry> undo = new();

    private readonly Stack<EditEntry> redo = new();

    public int Limit { get; set; } = 100;

    public bool CanUndo => undo.Count > 0;

    public bool CanRedo => redo.Count > 0;

    public void Record(EditEntry entry)
    {
        undo.AddLast(entry);
        while (undo.Count > Math.Max(Limit, 0))
        {
            undo.RemoveFirst();
        }

        redo.Clear();
    }

    public EditEntry? TakeUndo()
    {
        if (undo.Last is not { } node)
        {
            return null;
        }

        undo.RemoveLast();
        redo.Push(node.Value);
        return node.Value;
    }

    public EditEntry? TakeRedo()
    {
        if (!redo.TryPop(out var entry))
        {
            return null;
        }

        undo.AddLast(entry);
        return entry;
    }

    public void Clear()
    {
        undo.Clear();
        redo.Clear();
    }
}

internal abstract record EditEntry;

internal sealed record CellsEdited(IReadOnlyList<CellChange> Changes, EditArea? Area = null) : EditEntry;

// Corner rows and columns are held as objects, so the range still points at the same cells after
// rows or columns are inserted or deleted.
internal sealed record EditArea(SheetRow Top, SheetColumn Left, SheetRow Bottom, SheetColumn Right);

// Holds the inserted or deleted row/column objects themselves, so undo and redo move the same objects and the
// history entries that point at their cells stay valid. Before and After are the selections to restore; they are
// shifted if rows or columns moved in the meantime.
internal abstract record StructureEdited(bool Inserted, int Index, bool InheritFromNext, CellRange Before, CellRange After) : EditEntry
{
    // Whether the rows are in the sheet now (after an insert, or after undoing a delete). Rows the app removed first
    // are not put back by redo either.
    public bool Present { get; set; }

    public IReadOnlyList<SettingsRule>? RulesWith { get; set; }

    public IReadOnlyList<SettingsRule>? RulesWithout { get; set; }

    public IReadOnlyList<CellRange>? MergesWith { get; set; }

    public IReadOnlyList<CellRange>? MergesWithout { get; set; }
}

// Above is the row just above the block (null at the top); the block is put back directly below it
internal sealed record RowsEdited(bool Inserted, SheetRow? Above, IReadOnlyList<SheetRow> Rows, int Index, bool InheritFromNext, CellRange Before, CellRange After)
    : StructureEdited(Inserted, Index, InheritFromNext, Before, After);

// Left is the column just left of the block (null at the far left); the block is put back directly right of it
internal sealed record ColumnsEdited(bool Inserted, SheetColumn? Left, IReadOnlyList<SheetColumn> Columns, int Index, bool InheritFromNext, CellRange Before, CellRange After)
    : StructureEdited(Inserted, Index, InheritFromNext, Before, After)
{
    public IReadOnlyDictionary<SheetRow, CellData?[]>? Cells { get; set; }
}

// Row and column are held as objects, so the change still finds its cell after rows or columns shift
internal sealed record CellChange(SheetRow Row, SheetColumn Column, CellSnapshot Before, CellSnapshot After);

internal readonly record struct CellSnapshot(object? Value, string? Input, string? Error);
