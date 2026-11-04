namespace Example.Endpoints;

using Example.Application;

using Microsoft.AspNetCore.Localization;

// Both switches reload the page, so a new Server circuit or WebAssembly runtime starts with the new setting
public static class SettingEndpoints
{
    public static void MapSettingEndpoints(this WebApplication app)
    {
        app.MapGet("/render-mode/{mode}", HandleRenderMode);
        app.MapGet("/culture/{name}", HandleCulture);
    }

    private static IResult HandleRenderMode(string mode, string? redirect, HttpContext context)
    {
        SaveCookie(context, RenderModes.CookieName, mode == RenderModes.WebAssembly ? RenderModes.WebAssembly : RenderModes.Server);
        return RedirectBack(redirect);
    }

    private static IResult HandleCulture(string name, string? redirect, HttpContext context)
    {
        if (SupportedCultures.Names.Contains(name))
        {
            SaveCookie(context, CookieRequestCultureProvider.DefaultCookieName, CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(name)));
        }

        return RedirectBack(redirect);
    }

    private static void SaveCookie(HttpContext context, string name, string value) =>
        context.Response.Cookies.Append(name, value, new CookieOptions { Path = "/", MaxAge = TimeSpan.FromDays(30), IsEssential = true });

    private static IResult RedirectBack(string? redirect) =>
        Results.LocalRedirect(string.IsNullOrEmpty(redirect) || !redirect.StartsWith('/') ? "/" : redirect);
}
