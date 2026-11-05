namespace MusselSheet.E2ETests;

using System.Diagnostics;
using System.Net;
using System.Net.Sockets;

// Starts the Example app on a free port and opens it in an installed Edge or Chrome;
// Playwright's own browsers are not downloaded, and tests are skipped when neither is installed
public sealed class AppFixture : IAsyncLifetime
{
#if DEBUG
    private const string Configuration = "Debug";
#else
    private const string Configuration = "Release";
#endif

    private static readonly string[] Channels = ["msedge", "chrome"];

    private static readonly TimeSpan StartTimeout = TimeSpan.FromSeconds(60);

    private static readonly HttpClient Client = new();

    private readonly List<string> output = [];

    private Process? app;

    private IPlaywright? playwright;

    private IBrowser? browser;

    public string BaseAddress { get; private set; } = string.Empty;

    public async ValueTask InitializeAsync()
    {
        playwright = await Playwright.CreateAsync().ConfigureAwait(false);
        foreach (var channel in Channels)
        {
            try
            {
                browser = await playwright.Chromium.LaunchAsync(new() { Channel = channel }).ConfigureAwait(false);
                break;
            }
            catch (PlaywrightException)
            {
            }
        }

        if (browser is null)
        {
            return;
        }

        var port = FreePort();
        BaseAddress = string.Create(CultureInfo.InvariantCulture, $"http://127.0.0.1:{port}/");
        app = StartApp(port);
        await WaitForAppAsync(app).ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        if (browser is not null)
        {
            await browser.DisposeAsync().ConfigureAwait(false);
        }

        playwright?.Dispose();
        if (app is not null)
        {
            if (!app.HasExited)
            {
                app.Kill(entireProcessTree: true);
                await app.WaitForExitAsync().ConfigureAwait(false);
            }

            app.Dispose();
        }
    }

    // Goes through the render mode switch of the app, and waits until the sheet of the page is interactive
    public async Task<SamplePage> OpenAsync(string path, string mode)
    {
        var page = await OpenBlankAsync().ConfigureAwait(false);
        await page.Page.GotoAsync($"{BaseAddress}render-mode/{mode}?redirect={Uri.EscapeDataString($"/{path}")}").ConfigureAwait(false);
        await page.WaitForSheetAsync().ConfigureAwait(false);
        return page;
    }

    public async Task<SamplePage> OpenBlankAsync(bool javaScriptEnabled = true)
    {
        if (browser is null)
        {
            Assert.Skip("Neither Edge nor Chrome was found, so the E2E test is skipped.");
        }

        var context = await browser.NewContextAsync(new() { JavaScriptEnabled = javaScriptEnabled, ViewportSize = new() { Width = 1280, Height = 900 } }).ConfigureAwait(false);
        var page = await context.NewPageAsync().ConfigureAwait(false);
        return new SamplePage(context, page);
    }

    private static int FreePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }

    private static string FindSolutionDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while ((directory is not null) && !File.Exists(Path.Combine(directory.FullName, "MusselSheet.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("MusselSheet.slnx was not found.");
    }

    private Process StartApp(int port)
    {
        // Set to the output of dotnet publish to test the app as it ships, with the trimmed WebAssembly client
        var published = Environment.GetEnvironmentVariable("MUSSELSHEET_E2E_PUBLISH_DIRECTORY");
        var project = Path.Combine(FindSolutionDirectory(), "Example");
        var startInfo = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = published ?? project,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        startInfo.ArgumentList.Add(published is null ? Path.Combine(project, "bin", Configuration, "net10.0", "Example.dll") : Path.Combine(published, "Example.dll"));
        startInfo.ArgumentList.Add("--urls");
        startInfo.ArgumentList.Add(string.Create(CultureInfo.InvariantCulture, $"http://127.0.0.1:{port}"));
        startInfo.Environment["ASPNETCORE_ENVIRONMENT"] = published is null ? "Development" : "Production";

        var process = Process.Start(startInfo) ?? throw new InvalidOperationException("The app could not be started.");
        process.OutputDataReceived += (_, e) => Keep(e.Data);
        process.ErrorDataReceived += (_, e) => Keep(e.Data);
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        return process;
    }

    private async Task WaitForAppAsync(Process process)
    {
        var watch = Stopwatch.StartNew();
        while (true)
        {
            if (process.HasExited)
            {
                throw new InvalidOperationException($"The app has exited:{Environment.NewLine}{Log()}");
            }

            try
            {
                using var response = await Client.GetAsync(new Uri(BaseAddress)).ConfigureAwait(false);
                if (response.IsSuccessStatusCode)
                {
                    return;
                }
            }
            catch (HttpRequestException)
            {
            }

            if (watch.Elapsed > StartTimeout)
            {
                throw new TimeoutException($"The app does not respond:{Environment.NewLine}{Log()}");
            }

            await Task.Delay(200).ConfigureAwait(false);
        }
    }

    private void Keep(string? line)
    {
        if (line is null)
        {
            return;
        }

        lock (output)
        {
            output.Add(line);
            if (output.Count > 50)
            {
                output.RemoveAt(0);
            }
        }
    }

    private string Log()
    {
        lock (output)
        {
            return string.Join(Environment.NewLine, output);
        }
    }
}
