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

    public Sheet(int rows, int columns)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(rows);
        ArgumentOutOfRangeException.ThrowIfNegative(columns);
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
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);
            field = value;
            NotifyLayout();
        }
    }

    public int DefaultColumnWidth
    {
        get;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);
            field = value;
            NotifyLayout();
        }
    }

    public int FrozenRows
    {
        get;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value);
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
            ArgumentOutOfRangeException.ThrowIfNegative(value);
            if (field != value)
            {
                field = value;
                NotifyLayout();
            }
        }
    }

    public CultureInfo Culture
    {
        get;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            field = value;
            Raise(new SheetChangedEventArgs(SheetChangeKind.Reset));
        }
    }

    public SheetRange Cells => new(this, CellRange.All);

    internal IReadOnlyList<SettingsRule> Rules => settings.Rules;

    // Bumped by changes to settings, rows, columns and layout, but not by value changes, which are tracked per row
    // by SheetRow.ContentVersion.
    internal int StructureVersion { get; private set; }

    public Cell this[int row, int column]
    {
        get
        {
            CheckCell(row, column);
            return new Cell(this, row, column);
        }
    }

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
        ArgumentNullException.ThrowIfNull(configure);
        var patch = new CellSettings();
        configure(patch);
        AddRule(range, patch);
    }

    public void Configure(CellRange range, CellSettings patch)
    {
        ArgumentNullException.ThrowIfNull(patch);
        AddRule(range, patch.Clone());
    }

    public ResolvedCellSettings GetSettings(int row, int column)
    {
        CheckCell(row, column);
        return settings.Resolve(row, column);
    }

    internal CellEdges GetEdges(int row, int column)
    {
        CheckCell(row, column);
        return settings.ResolveEdges(row, column, column + 1 < ColumnCount, row + 1 < RowCount);
    }

    public IDisposable BeginUpdate()
    {
        updateDepth++;
        return new UpdateScope(this);
    }

    internal void NotifyLayout() => Raise(new SheetChangedEventArgs(SheetChangeKind.Layout));

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

    private void CheckCell(int row, int column)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(row);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(row, RowCount);
        ArgumentOutOfRangeException.ThrowIfNegative(column);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(column, ColumnCount);
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
