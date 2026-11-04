using Example.Application;

//--------------------------------------------------------------------------------
// Configure builder
//--------------------------------------------------------------------------------
var builder = WebApplication.CreateBuilder(args);

// API
builder.ConfigureApi();

// Blazor
builder.ConfigureBlazor();

//--------------------------------------------------------------------------------
// Configure the HTTP request pipeline
//--------------------------------------------------------------------------------
var app = builder.Build();

// Development
app.UseDevelopmentTools();

// Localization
app.UseLocalization();

// Error handler
app.UseErrorHandler();

// Routing
app.UseRouting();

// Antiforgery
app.UseAntiforgery();

// End point
app.MapEndpoints();

// Run
await app.RunAsync();
