using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DiscordOverlay.App.Hosting;

public sealed class TrayApplicationContext : ApplicationContext
{
    private readonly ILogger<TrayApplicationContext> logger;

    public TrayApplicationContext(
        ILogger<TrayApplicationContext> logger,
        IHostApplicationLifetime lifetime)
    {
        this.logger = logger;
        lifetime.ApplicationStopping.Register(OnHostStopping);
    }

    private void OnHostStopping()
    {
        logger.LogInformation("Host stopping — exiting Windows Forms message loop");
        Application.Exit();
    }
}
