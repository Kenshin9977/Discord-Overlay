using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace DiscordOverlay.Core.Discord;

public static class DiscordServiceCollectionExtensions
{
    public static IServiceCollection AddDiscordRpcClient(this IServiceCollection services)
    {
        services.TryAddSingleton<IDiscordIpcTransport, NamedPipeIpcTransport>();
        services.TryAddSingleton<IDiscordRpcClient, DiscordRpcClient>();
        return services;
    }
}
