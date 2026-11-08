namespace Example.Client.Components.Samples;

using Microsoft.AspNetCore.Components;

public sealed partial class SourceView
{
    private IReadOnlyList<(string Name, string Text)> files = [];

    private Type? loaded;

    [Parameter]
    [EditorRequired]
    public Type Page { get; set; } = default!;

    [Parameter]
    public IReadOnlyList<string> ExtraFiles { get; set; } = [];

    protected override void OnParametersSet()
    {
        if (loaded != Page)
        {
            loaded = Page;
            files = [.. new[] { $"{Page.Name}.razor", $"{Page.Name}.razor.cs" }.Concat(ExtraFiles).Select(static x => (x, Read(x)))];
        }
    }

    private static string Read(string name)
    {
        using var stream = typeof(SourceView).Assembly.GetManifestResourceStream($"Source/{name}") ?? throw new InvalidOperationException($"{name} is not embedded.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd().ReplaceLineEndings("\n");
    }
}
