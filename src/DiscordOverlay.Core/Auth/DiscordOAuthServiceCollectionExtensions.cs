using System.Runtime.Versioning;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace DiscordOverlay.Core.Auth;

public static class DiscordOAuthServiceCollectionExtensions
{
    public static IServiceCollection AddDiscordOAuth(this IServiceCollection services)
    {
        services.AddHttpClient<IDiscordTokenExchange, DiscordTokenExchange>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(15);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Discord-Overlay (https://github.com/Kenshin9977/Discord-Overlay)");
        });
        return services;
    }

    [SupportedOSPlatform("windows")]
    public static IServiceCollection AddDpapiCredentialStore(this IServiceCollection services)
    {
        services.AddOptions<DiscordCredentialStoreOptions>();
        services.TryAddSingleton<IDiscordCredentialStore, DpapiDiscordCredentialStore>();
        return services;
    }

    public static IServiceCollection AddDiscordSession(this IServiceCollection services)
    {
        services.TryAddSingleton<IDiscordSession, DiscordSession>();
        return services;
    }
}
