namespace MusselSheet;

using System.ComponentModel;
using System.Diagnostics;

public sealed class JavaScriptTests
{
    [Fact]
    public async Task ScriptFunctionsPassTheirTests()
    {
        var startInfo = new ProcessStartInfo("node")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            WorkingDirectory = FindSolutionDirectory()
        };
        startInfo.ArgumentList.Add("--test");
        startInfo.ArgumentList.Add(Path.Combine("MusselSheet.Tests", "js", "MusselSheet.test.mjs"));

        using var process = StartProcess(startInfo);
        if (process is null)
        {
            Assert.Skip("node が見つからないため、JS のテストを省略しました");
            return;
        }

        var token = Xunit.TestContext.Current.CancellationToken;
        var output = process.StandardOutput.ReadToEndAsync(token);
        var errors = process.StandardError.ReadToEndAsync(token);
        await process.WaitForExitAsync(token).ConfigureAwait(true);
        var log = await output.ConfigureAwait(true) + await errors.ConfigureAwait(true);

        Assert.True(process.ExitCode == 0, log);
    }

    private static Process? StartProcess(ProcessStartInfo startInfo)
    {
        try
        {
            return Process.Start(startInfo);
        }
        catch (Win32Exception)
        {
            return null;
        }
    }

    private static string FindSolutionDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while ((directory is not null) && !File.Exists(Path.Combine(directory.FullName, "MusselSheet.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("MusselSheet.slnx が見つかりません");
    }
}
