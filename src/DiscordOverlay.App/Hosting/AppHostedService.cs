using System.Diagnostics;
using System.IO;
using System.Windows;
using DiscordOverlay.App.Resources;
using DiscordOverlay.App.Settings;
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
    IOptionsMonitor<StreamKitOverlayOptions> overlayOptions,
    AutoStartManager autoStart,
    IUiDispatcher uiDispatcher,
    IHostApplicationLifetime lifetime,
    ILogger<AppHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await session.ResumeFromStoreAsync(stoppingToken).ConfigureAwait(false);

            if (NeedsFirstRunSetup())
            {
                if (!await RunSetupDialogAsync(stoppingToken).ConfigureAwait(false))
                {
                    return;
                }
            }
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

    private bool NeedsFirstRunSetup()
    {
        if (session.Current is null)
        {
            return true;
        }

        // settings.json missing or OBS WebSocket password unset means the
        // user hasn't completed the OBS half of setup yet.
        if (!File.Exists(AppConfigStore.DefaultFilePath))
        {
            return true;
        }

        var current = obsOptions.CurrentValue;
        return string.IsNullOrEmpty(current.Password);
    }

    private async Task<bool> RunSetupDialogAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Launching first-run setup dialog");

        var result = await uiDispatcher.InvokeAsync(() =>
        {
            // No push callback here: OBS is not configured yet at first run, so
            // an appearance change can only land in the file and be carried by
            // the first push after setup.
            var window = new SettingsWindow(
                session,
                obsOptions.CurrentValue,
                autoStart,
                obsTester,
                () => overlayOptions.CurrentValue);
            return window.ShowDialog();
        }).ConfigureAwait(false);

        if (cancellationToken.IsCancellationRequested) return false;

        if (result == true)
        {
            logger.LogInformation("First-run setup completed; restarting to apply OBS settings");
            await uiDispatcher.InvokeAsync(async () =>
            {
                var box = new Wpf.Ui.Controls.MessageBox
                {
                    Title = Strings.AppName,
                    Content = Strings.AppRestartMessage,
                    CloseButtonText = Strings.DialogClose,
                };
                await box.ShowDialogAsync().ConfigureAwait(true);

                // WPF has no Application.Restart. Relaunch this exe and let the
                // host shut down behind us.
                var exe = Environment.ProcessPath;
                if (!string.IsNullOrEmpty(exe))
                {
                    Process.Start(new ProcessStartInfo { FileName = exe, UseShellExecute = true });
                }
                Application.Current?.Shutdown();
            }).ConfigureAwait(false);
            return true;
        }

        logger.LogInformation("First-run setup cancelled — exiting application");
        lifetime.StopApplication();
        return false;
    }
}
