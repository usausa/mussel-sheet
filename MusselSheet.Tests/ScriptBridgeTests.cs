namespace MusselSheet;

using System.Reflection;
using System.Text.RegularExpressions;

using Microsoft.JSInterop;

using MusselSheet.Internal;

public sealed partial class ScriptBridgeTests
{
    [Fact]
    public void ScriptEntryPointsStayOutOfThePublicApi()
    {
        var entryPoints = typeof(MusselSheetView).Assembly.GetExportedTypes()
            .SelectMany(static x => x.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
            .Where(static x => x.IsDefined(typeof(JSInvokableAttribute), false))
            .Select(static x => $"{x.DeclaringType!.Name}.{x.Name}");

        Assert.Empty(entryPoints);
    }

    [Fact]
    public void EveryCallFromTheScriptHasAnEntryPoint()
    {
        var script = File.ReadAllText(Path.Combine(TestPaths.Root, "MusselSheet", "wwwroot", "MusselSheet.js"));
        var names = Calls().Matches(script).Select(static x => x.Groups["name"].Value).Distinct().ToList();
        var entryPoints = typeof(SheetScriptBridge).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(static x => x.IsDefined(typeof(JSInvokableAttribute), false))
            .Select(static x => x.Name)
            .ToList();

        Assert.NotEmpty(names);
        Assert.All(names, name => Assert.Contains(name, entryPoints));
    }

    [GeneratedRegex(@"(?:invokeMethodAsync|send|sendChange|runRange|fillFrom)\(\s*'(?<name>\w+)'")]
    private static partial Regex Calls();
}
