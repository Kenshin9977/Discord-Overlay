using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace DiscordOverlay.Core.Discord;

public static class DiscordServiceCollectionExtensions
{
    public static IServiceCollection AddDiscordRpcClient(this IServiceCollection services)
    {
        services.TryAddSingleton<IDiscordIpcTransport, NamedPipeIpcTransport>();
        services.TryAddSingleton<IDiscordRpcClient, DiscordRpcClient>();
        return services;
    }

    public static IServiceCollection AddDiscordVoiceChannelWatcher(this IServiceCollection services)
    {
        services.AddOptions<DiscordVoiceChannelWatcherOptions>();
        services.TryAddSingleton<DiscordVoiceChannelWatcher>();
        services.TryAddSingleton<IDiscordVoiceChannelWatcher>(
            sp => sp.GetRequiredService<DiscordVoiceChannelWatcher>());
        services.AddHostedService(sp => sp.GetRequiredService<DiscordVoiceChannelWatcher>());
        return services;
    }
}
