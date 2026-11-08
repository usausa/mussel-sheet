namespace MusselSheet;

using System.Globalization;

using MusselSheet.Internal;

public sealed partial class Sheet
{
    private readonly List<SheetRow> rowList = [];

    private readonly List<SheetColumn> columnList = [];

    private readonly SettingsStore settings = new();

    private int updateDepth;

    private bool pendingReset;

    private MusselSheetOptions? registeredOptions;

    public Sheet(int rows, int columns)
    {
        DefaultRowHeight = 24;
        DefaultColumnWidth = 100;
        Culture = DefaultCulture();
        for (var c = 0; c < columns; c++)
        {
            columnList.Add(new SheetColumn(this, null));
        }

        for (var r = 0; r < rows; r++)
        {
            rowList.Add(new SheetRow(this, columns, null));
        }

        Reindex();
    }

    public event EventHandler<SheetChangedEventArgs>? Changed;

    public int RowCount => rowList.Count;

    public int ColumnCount => columnList.Count;

    public IReadOnlyList<SheetRow> Rows => rowList;

    public IReadOnlyList<SheetColumn> Columns => columnList;

    public int DefaultRowHeight
    {
        get;
        set
        {
            field = value;
            NotifyLayout();
        }
    }

    public int DefaultColumnWidth
    {
        get;
        set
        {
            field = value;
            NotifyLayout();
        }
    }

    public int FrozenRows
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                NotifyLayout();
            }
        }
    }

    public int FrozenColumns
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                NotifyLayout();
            }
        }
    }

    // Not set, the sheet uses the built-in languages until a view shows it with registered options
    public MusselSheetOptions? Options
    {
        get;
        set
        {
            field = value;
            ClearInputTexts();
            Raise(new SheetChangedEventArgs(SheetChangeKind.Reset));
        }
    }

    public CultureInfo Culture
    {
        get;
        set
        {
            field = ReadingRules.Gregorian(value);
            ClearInputTexts();
            Raise(new SheetChangedEventArgs(SheetChangeKind.Reset));
        }
    }

    public SheetRange Cells => new(this, CellRange.All);

    internal MusselSheetOptions EffectiveOptions => Options ?? registeredOptions ?? MusselSheetOptions.Default;

    internal IReadOnlyList<SettingsRule> Rules => settings.Rules;

    // Bumped by changes to settings, rows, columns and layout, but not by value changes, which are tracked per row
    // by SheetRow.ContentVersion.
    internal int StructureVersion { get; private set; }

    public Cell this[int row, int column] => new(this, row, column);

    public Cell this[string address]
    {
        get
        {
            var parsed = CellAddress.Parse(address);
            return this[parsed.Row, parsed.Column];
        }
    }

    public SheetRange Range(string range) => new(this, CellRange.Parse(range));

    public SheetRange Range(CellRange range) => new(this, range);

    public void Configure(CellRange range, Action<CellSettings> configure)
    {
        var patch = new CellSettings();
        configure(patch);
        AddRule(range, patch);
    }

    public void Configure(CellRange range, CellSettings patch) => AddRule(range, patch.Clone());

    public ResolvedCellSettings GetSettings(int row, int column) => settings.Resolve(row, column);

    internal CellEdges GetEdges(int row, int column) => settings.ResolveEdges(row, column, column + 1 < ColumnCount, row + 1 < RowCount);

    public IDisposable BeginUpdate()
    {
        updateDepth++;
        return new UpdateScope(this);
    }

    internal void NotifyLayout() => Raise(new SheetChangedEventArgs(SheetChangeKind.Layout));

    // Not a change of the sheet: a view calls this before it first shows the sheet
    internal void UseRegisteredOptions(MusselSheetOptions? registered) => registeredOptions ??= registered;

    private static CultureInfo DefaultCulture() => CultureInfo.CurrentCulture;

    private void AddRule(CellRange range, CellSettings patch)
    {
        settings.Add(range, patch);
        Raise(new SheetChangedEventArgs(SheetChangeKind.Settings, range));
    }

    private void Raise(SheetChangedEventArgs e)
    {
        if (e.Kind is not (SheetChangeKind.Values or SheetChangeKind.Errors))
        {
            StructureVersion++;
        }

        NotifyBindings(e);
        NotifyComputations(e);
        if (updateDepth > 0)
        {
            pendingReset = true;
            return;
        }

        Changed?.Invoke(this, e);
    }

    private void EndUpdate()
    {
        updateDepth--;

        RunComputations();
        if ((updateDepth == 0) && pendingReset)
        {
            pendingReset = false;
            Changed?.Invoke(this, new SheetChangedEventArgs(SheetChangeKind.Reset));
        }
    }

    private void Reindex()
    {
        for (var r = 0; r < rowList.Count; r++)
        {
            rowList[r].Index = r;
        }

        for (var c = 0; c < columnList.Count; c++)
        {
            columnList[c].Index = c;
        }
    }

    private sealed class UpdateScope : IDisposable
    {
        private Sheet? sheet;

        public UpdateScope(Sheet owner)
        {
            sheet = owner;
        }

        public void Dispose()
        {
            sheet?.EndUpdate();
            sheet = null;
        }
    }
}
