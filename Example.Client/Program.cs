using Example.Client;

using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.JSInterop;

using MudBlazor.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
#if EXAMPLE_STANDALONE
builder.RootComponents.Add<Example.Client.Components.Routes>("#app");
builder.RootComponents.Add<Microsoft.AspNetCore.Components.Web.HeadOutlet>("head::after");
builder.Services.AddScoped(_ => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });
#endif

builder.Services.AddMudServices();
builder.Services.AddMusselSheet(SheetOptionsSetup.Configure);

var host = builder.Build();

string? name = null;
try
{
    name = await host.Services.GetRequiredService<IJSRuntime>().InvokeAsync<string?>("exampleCulture");
}
catch (JSDisconnectedException)
{
}

var culture = CultureInfo.GetCultureInfo(name ?? "en-US");
CultureInfo.DefaultThreadCurrentCulture = culture;
CultureInfo.DefaultThreadCurrentUICulture = culture;

await host.RunAsync();
