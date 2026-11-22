namespace Example.Client.Components.Samples;

using Microsoft.AspNetCore.Components;

public sealed partial class SampleLog : IDisposable
{
    private SheetEventLog? watched;

    private bool pending;

    [Parameter]
    [EditorRequired]
    public SheetEventLog Log { get; set; } = default!;

    [Parameter]
    public RenderFragment? Output { get; set; }

    public void Dispose()
    {
        if (watched is not null)
        {
            watched.Changed -= OnLogChanged;
        }
    }

    protected override void OnParametersSet()
    {
        if (ReferenceEquals(watched, Log))
        {
            return;
        }

        Dispose();
        watched = Log;
        watched.Changed += OnLogChanged;
    }

    private void OnLogChanged(object? sender, EventArgs e)
    {
        if (pending)
        {
            return;
        }

        pending = true;
        _ = RenderLaterAsync();
    }

    private async Task RenderLaterAsync()
    {
        await Task.Yield();
        pending = false;
        await InvokeAsync(StateHasChanged);
    }
}
