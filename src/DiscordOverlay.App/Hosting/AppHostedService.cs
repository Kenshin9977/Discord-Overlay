using DiscordOverlay.App.Setup;
using DiscordOverlay.Core.Auth;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DiscordOverlay.App.Hosting;

public sealed class AppHostedService(
    IDiscordSession session,
    IUiDispatcher uiDispatcher,
    IHostApplicationLifetime lifetime,
    ILogger<AppHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            var resumed = await session.ResumeFromStoreAsync(stoppingToken).ConfigureAwait(false);
            if (resumed)
            {
                return;
            }

            await RunSetupWizardAsync(stoppingToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // graceful shutdown
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Startup orchestration failed");
        }
    }

    private async Task RunSetupWizardAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Launching first-run setup wizard");

        var result = await uiDispatcher.InvokeAsync(() =>
        {
            using var wizard = new SetupWizardForm(session);
            return wizard.ShowDialog();
        }).ConfigureAwait(false);

        if (cancellationToken.IsCancellationRequested) return;

        if (result == DialogResult.OK)
        {
            logger.LogInformation("Setup wizard completed; Discord session is now active");
        }
        else
        {
            logger.LogInformation("Setup wizard cancelled — exiting application");
            lifetime.StopApplication();
        }
    }
}
