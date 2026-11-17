namespace MusselSheet;

public sealed class CellEditContext
{
    private readonly Func<CellEditContext, Task<bool>> commit;

    private readonly Func<Task> cancel;

    private readonly SheetRow row;

    private readonly SheetColumn column;

    public Sheet Sheet { get; }

    public CellAddress Address => new(row.Index, column.Index);

    public string? Name { get; }

    public object? Value { get; set; }

    public string? Error { get; internal set; }

    internal CellEditContext(Sheet sheet, SheetRow row, SheetColumn column, string? name, object? value, Func<CellEditContext, Task<bool>> commit, Func<Task> cancel)
    {
        Sheet = sheet;
        this.row = row;
        this.column = column;
        Name = name;
        Value = value;
        this.commit = commit;
        this.cancel = cancel;
    }

    internal bool IsCurrent => Sheet.Contains(row) && Sheet.Contains(column);

    public Task<bool> CommitAsync() => commit(this);

    public Task<bool> CommitAsync(object? value)
    {
        Value = value;
        return commit(this);
    }

    public Task CancelAsync() => cancel();
}
