using DiscordOverlay.App.Setup;
using DiscordOverlay.Core;
using DiscordOverlay.Core.Auth;
using DiscordOverlay.Core.Streaming;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DiscordOverlay.App.Hosting;

public sealed class AppHostedService(
    IDiscordSession session,
    ObsConnectionTester obsTester,
    IOptionsMonitor<ObsConnectionOptions> obsOptions,
    IUiDispatcher uiDispatcher,
    IHostApplicationLifetime lifetime,
    ILogger<AppHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            var resumed = await session.ResumeFromStoreAsync(stoppingToken).ConfigureAwait(false);
            if (!resumed)
            {
                if (!await RunDiscordWizardAsync(stoppingToken).ConfigureAwait(false))
                {
                    return;
                }
            }

            await EnsureObsConfiguredAsync(stoppingToken).ConfigureAwait(false);
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

    private async Task<bool> RunDiscordWizardAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Launching Discord setup wizard");

        var result = await uiDispatcher.InvokeAsync(() =>
        {
            using var wizard = new SetupWizardForm(session);
            return wizard.ShowDialog();
        }).ConfigureAwait(false);

        if (cancellationToken.IsCancellationRequested) return false;

        if (result == DialogResult.OK)
        {
            logger.LogInformation("Discord setup completed");
            return true;
        }

        logger.LogInformation("Discord setup cancelled — exiting application");
        lifetime.StopApplication();
        return false;
    }

    private async Task EnsureObsConfiguredAsync(CancellationToken cancellationToken)
    {
        // If the user has never run the OBS step (no settings.json), or
        // OBS settings are clearly empty, walk them through it. Otherwise
        // we let the existing values drive ObsBrowserSourceUpdater and
        // assume any tweaks happen via the post-install Settings dialog.
        var settingsExists = File.Exists(AppConfigStore.DefaultFilePath);
        var current = obsOptions.CurrentValue;

        if (settingsExists && !string.IsNullOrEmpty(current.Password))
        {
            return;
        }

        logger.LogInformation("Launching OBS setup wizard");

        var result = await uiDispatcher.InvokeAsync(() =>
        {
            using var wizard = new ObsSetupForm(obsTester, current);
            return wizard.ShowDialog();
        }).ConfigureAwait(false);

        if (cancellationToken.IsCancellationRequested) return;

        if (result == DialogResult.OK)
        {
            logger.LogInformation("OBS setup completed; restarting to apply OBS WebSocket settings");
            await uiDispatcher.InvokeAsync(() =>
            {
                MessageBox.Show(
                    "Settings saved. Discord-Overlay will now restart so OBS settings take effect.",
                    "Discord-Overlay",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                Application.Restart();
            }).ConfigureAwait(false);
        }
        else
        {
            logger.LogInformation(
                "OBS setup skipped — overlay updates will not be pushed until OBS is configured via Settings…");
        }
    }
}
