using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DiscordOverlay.App.Hosting;

public sealed class AppHostedService(ILogger<AppHostedService> logger) : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("AppHostedService running");
        stoppingToken.Register(() => logger.LogInformation("AppHostedService stop requested"));
        return Task.CompletedTask;
    }
}
