namespace MusselSheet;

public sealed class CellEditContext
{
    private readonly Func<CellEditContext, Task<bool>> commit;

    private readonly Func<Task> cancel;

    public Sheet Sheet { get; }

    public CellAddress Address { get; }

    public string? Name { get; }

    public object? Value { get; set; }

    public string? Error { get; internal set; }

    internal CellEditContext(Sheet sheet, CellAddress address, string? name, object? value, Func<CellEditContext, Task<bool>> commit, Func<Task> cancel)
    {
        Sheet = sheet;
        Address = address;
        Name = name;
        Value = value;
        this.commit = commit;
        this.cancel = cancel;
    }

    public Task<bool> CommitAsync() => commit(this);

    public Task<bool> CommitAsync(object? value)
    {
        Value = value;
        return commit(this);
    }

    public Task CancelAsync() => cancel();
}
