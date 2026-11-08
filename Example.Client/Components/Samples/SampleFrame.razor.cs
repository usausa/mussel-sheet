namespace Example.Client.Components.Samples;

using Microsoft.AspNetCore.Components;

public sealed partial class SampleFrame
{
    [Parameter]
    [EditorRequired]
    public string Title { get; set; } = default!;

    [Parameter]
    [EditorRequired]
    public Type Page { get; set; } = default!;

    [Parameter]
    public SheetEventLog? Log { get; set; }

    [Parameter]
    public RenderFragment? Description { get; set; }

    [Parameter]
    public RenderFragment? Controls { get; set; }

    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    [Parameter]
    public RenderFragment? Output { get; set; }

    [Parameter]
    public IReadOnlyList<string> ExtraFiles { get; set; } = [];
}
