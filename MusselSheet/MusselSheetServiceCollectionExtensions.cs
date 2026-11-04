namespace MusselSheet;

using Microsoft.Extensions.DependencyInjection;

public static class MusselSheetServiceCollectionExtensions
{
    public static IServiceCollection AddMusselSheet(this IServiceCollection services, Action<MusselSheetOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        var options = new MusselSheetOptions();
        configure?.Invoke(options);
        return services.AddSingleton(options);
    }
}
