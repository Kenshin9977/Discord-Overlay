using Microsoft.Extensions.DependencyInjection;

namespace DiscordOverlay.Core.Auth;

public static class DiscordOAuthServiceCollectionExtensions
{
    public static IServiceCollection AddDiscordOAuth(this IServiceCollection services)
    {
        services.AddHttpClient<IDiscordTokenExchange, DiscordTokenExchange>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(15);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Discord-Overlay (https://github.com/kenshin993355/Discord-Overlay)");
        });
        return services;
    }
}
