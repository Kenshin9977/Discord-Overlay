using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace DiscordOverlay.Core.Streaming;

public static class StreamingServiceCollectionExtensions
{
    public static IServiceCollection AddObsBrowserSourceUpdater(this IServiceCollection services)
    {
        services.AddOptions<StreamKitOverlayOptions>();
        services.AddOptions<ObsConnectionOptions>();
        services.TryAddSingleton<StreamKitUrlBuilder>();
        services.TryAddSingleton<ObsBrowserSourceUpdater>();
        services.AddHostedService(sp => sp.GetRequiredService<ObsBrowserSourceUpdater>());
        return services;
    }
}
