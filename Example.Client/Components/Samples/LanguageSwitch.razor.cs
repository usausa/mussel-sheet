namespace Example.Client.Components.Samples;

using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

public sealed partial class LanguageSwitch
{
#if EXAMPLE_STANDALONE
    private static bool Standalone => true;
#else
    private static bool Standalone => false;
#endif

    private static bool IsJapanese => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ja";

    [Inject]
    private NavigationManager Navigation { get; set; } = default!;

    [Inject]
    private IJSRuntime Runtime { get; set; } = default!;

    private string? Link(string culture) => Standalone ? null : $"culture/{culture}?redirect={Uri.EscapeDataString($"/{Navigation.ToBaseRelativePath(Navigation.Uri)}")}";

    private async Task SelectAsync(string culture)
    {
        if (Standalone)
        {
            try
            {
                await Runtime.InvokeVoidAsync("exampleSaveCulture", culture);
            }
            catch (JSDisconnectedException)
            {
            }
        }
    }
}
