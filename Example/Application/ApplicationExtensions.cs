namespace Example.Application;

using Example.Client;
using Example.Client.Components;
using Example.Components;
using Example.Endpoints;

using Microsoft.AspNetCore.Localization;

using MudBlazor.Services;

public static class ApplicationExtensions
{
    //--------------------------------------------------------------------------------
    // API
    //--------------------------------------------------------------------------------

    public static IHostApplicationBuilder ConfigureApi(this IHostApplicationBuilder builder)
    {
        builder.Services.AddProblemDetails();

        return builder;
    }

    //--------------------------------------------------------------------------------
    // Blazor
    //--------------------------------------------------------------------------------

    public static IHostApplicationBuilder ConfigureBlazor(this IHostApplicationBuilder builder)
    {
        // Razor components
        builder.Services
            .AddRazorComponents()
            .AddInteractiveServerComponents(options =>
            {
                options.DetailedErrors = builder.Environment.IsDevelopment();
            })
            .AddInteractiveWebAssemblyComponents();

        // MudBlazor
        builder.Services.AddMudServices();

        // MusselSheet
        builder.Services.AddMusselSheet(SheetOptionsSetup.Configure);

        return builder;
    }

    //--------------------------------------------------------------------------------
    // Development
    //--------------------------------------------------------------------------------

    public static WebApplication UseDevelopmentTools(this WebApplication app)
    {
        if (app.Environment.IsDevelopment())
        {
            app.UseWebAssemblyDebugging();
        }

        return app;
    }

    //--------------------------------------------------------------------------------
    // Localization
    //--------------------------------------------------------------------------------

    public static WebApplication UseLocalization(this WebApplication app)
    {
        // The language comes from the cookie of the Localization page, not from the browser
        app.UseRequestLocalization(new RequestLocalizationOptions
        {
            DefaultRequestCulture = new RequestCulture(SupportedCultures.Names[0]),
            SupportedCultures = [.. SupportedCultures.Names.Select(CultureInfo.GetCultureInfo)],
            SupportedUICultures = [.. SupportedCultures.Names.Select(CultureInfo.GetCultureInfo)],
            RequestCultureProviders = [new CookieRequestCultureProvider()],
        });

        return app;
    }

    //--------------------------------------------------------------------------------
    // Error handler
    //--------------------------------------------------------------------------------

    public static WebApplication UseErrorHandler(this WebApplication app)
    {
        // API: ProblemDetails
        app.UseWhen(
            static context => context.Request.Path.StartsWithSegments(ApiRoutes.Prefix, StringComparison.OrdinalIgnoreCase),
            static b =>
            {
                b.UseExceptionHandler();
                b.UseStatusCodePages();
            });

        // Page: error page and not found page
        app.UseWhen(
            static context => !context.Request.Path.StartsWithSegments(ApiRoutes.Prefix, StringComparison.OrdinalIgnoreCase),
            static b =>
            {
                b.UseExceptionHandler("/error", createScopeForErrors: true);
                b.UseWhen(
                    static context => HttpMethods.IsGet(context.Request.Method),
                    static g => g.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true));
            });

        return app;
    }

    //--------------------------------------------------------------------------------
    // End point
    //--------------------------------------------------------------------------------

    public static WebApplication MapEndpoints(this WebApplication app)
    {
        // Static assets
        app.MapStaticAssets();

        // Blazor
        app.MapRazorComponents<App>()
            .AddInteractiveServerRenderMode()
            .AddInteractiveWebAssemblyRenderMode()
            .AddAdditionalAssemblies(typeof(Routes).Assembly);

        // Settings
        app.MapSettingEndpoints();

        // API
        app.MapReportEndpoints();

        return app;
    }
}
