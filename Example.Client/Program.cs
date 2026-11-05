using Example.Client;

using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.JSInterop;

using MudBlazor.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.Services.AddMudServices();
builder.Services.AddMusselSheet(SheetOptionsSetup.Configure);

var host = builder.Build();

var name = await host.Services.GetRequiredService<IJSRuntime>().InvokeAsync<string?>("exampleCulture");
var culture = CultureInfo.GetCultureInfo(name ?? "en-US");
CultureInfo.DefaultThreadCurrentCulture = culture;
CultureInfo.DefaultThreadCurrentUICulture = culture;

await host.RunAsync();
