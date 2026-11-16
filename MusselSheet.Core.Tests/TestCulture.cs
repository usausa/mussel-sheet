namespace MusselSheet;

using System.Runtime.CompilerServices;

internal static class TestCulture
{
    [ModuleInitializer]
    internal static void Initialize()
    {
        var japanese = CultureInfo.GetCultureInfo("ja-JP");
        CultureInfo.DefaultThreadCurrentCulture = japanese;
        CultureInfo.DefaultThreadCurrentUICulture = japanese;
        CultureInfo.CurrentCulture = japanese;
        CultureInfo.CurrentUICulture = japanese;
    }
}
