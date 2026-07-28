using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using DiscordOverlay.App.Resources;
using DiscordOverlay.App.Settings;
using DiscordOverlay.Core.Auth;
using DiscordOverlay.Core.Discord;
using DiscordOverlay.Core.Streaming;
using H.NotifyIcon;
using H.NotifyIcon.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OBSStudioClient.Enums;
using Wpf.Ui.Controls;
// Wpf.Ui.Controls redefines both of these. The menu wants the plain WPF
// MenuItem — the Fluent one is for NavigationView — and the message box wants
// the Fluent one.
using MenuItem = System.Windows.Controls.MenuItem;
using MessageBox = Wpf.Ui.Controls.MessageBox;
using Separator = System.Windows.Controls.Separator;

namespace DiscordOverlay.App.Hosting;

/// <summary>
/// The notification-area icon and its menu — the app's only persistent UI.
/// </summary>
public sealed class TrayApplication : IDisposable
{
    private const int TooltipMaxLength = 127;
    private static readonly TimeSpan StatusRefreshInterval = TimeSpan.FromSeconds(1);

    private readonly ILogger<TrayApplication> logger;
    private readonly IHostApplicationLifetime lifetime;
    private readonly IDiscordVoiceChannelWatcher watcher;
    private readonly ObsBrowserSourceUpdater obsUpdater;
    private readonly ObsConnectionTester obsTester;
    private readonly IDiscordSession session;
    private readonly IOptionsMonitor<ObsConnectionOptions> obsOptions;
    private readonly IOptionsMonitor<StreamKitOverlayOptions> overlayOptions;
    private readonly IConfiguration configuration;
    private readonly AutoStartManager autoStart;
    private readonly AppUpdater updater;

    private readonly TaskbarIcon trayIcon;
    private readonly DispatcherTimer statusTimer;
    private readonly MenuItem channelStatusItem;
    private readonly MenuItem obsStatusItem;

    private SettingsWindow? settingsWindow;
    private bool disposed;

    public TrayApplication(
        ILogger<TrayApplication> logger,
        IHostApplicationLifetime lifetime,
        IDiscordVoiceChannelWatcher watcher,
        ObsBrowserSourceUpdater obsUpdater,
        ObsConnectionTester obsTester,
        IDiscordSession session,
        IOptionsMonitor<ObsConnectionOptions> obsOptions,
        IOptionsMonitor<StreamKitOverlayOptions> overlayOptions,
        IConfiguration configuration,
        AutoStartManager autoStart,
        AppUpdater updater)
    {
        this.logger = logger;
        this.lifetime = lifetime;
        this.watcher = watcher;
        this.obsUpdater = obsUpdater;
        this.obsTester = obsTester;
        this.session = session;
        this.obsOptions = obsOptions;
        this.overlayOptions = overlayOptions;
        this.configuration = configuration;
        this.autoStart = autoStart;
        this.updater = updater;

        channelStatusItem = new MenuItem { Header = Strings.TrayChannelStarting, IsEnabled = false };
        obsStatusItem = new MenuItem { Header = Strings.TrayObsStarting, IsEnabled = false };

        var menu = new ContextMenu();
        menu.Items.Add(channelStatusItem);
        menu.Items.Add(obsStatusItem);
        menu.Items.Add(new Separator());
        menu.Items.Add(Item(Strings.TrayMenuSettings, OnSettingsClicked));
        menu.Items.Add(Item(Strings.TrayMenuOverlayAppearance, OnOverlayAppearanceClicked));

        // Hidden rather than disabled in the Store edition. A greyed-out
        // "Check for updates" invites the user to wonder what is broken; its
        // absence reads as "something else handles this", which is true.
        if (AppUpdater.SelfUpdatesAllowed)
        {
            menu.Items.Add(Item(Strings.TrayMenuCheckUpdates, OnCheckForUpdatesClicked));
        }
        menu.Items.Add(Item(Strings.TrayMenuOpenLogFolder, OnOpenLogFolderClicked));
        menu.Items.Add(new Separator());
        menu.Items.Add(Item(Strings.TrayMenuQuit, OnQuitClicked));

        trayIcon = new TaskbarIcon
        {
            IconSource = new BitmapImage(
                new Uri("pack://application:,,,/Discord-Overlay.ico", UriKind.Absolute)),
            ToolTipText = Strings.TrayInitialTooltip,
            ContextMenu = menu,
            MenuActivation = PopupActivationMode.RightClick,
            NoLeftClickDelay = true,
        };
        trayIcon.TrayMouseDoubleClick += (_, _) => RefreshStatus();
        trayIcon.ForceCreate();

        statusTimer = new DispatcherTimer { Interval = StatusRefreshInterval };
        statusTimer.Tick += (_, _) => RefreshStatus();
        statusTimer.Start();
        RefreshStatus();

        lifetime.ApplicationStopping.Register(OnHostStopping);
    }

    private static MenuItem Item(string header, RoutedEventHandler onClick)
    {
        var item = new MenuItem { Header = header };
        item.Click += onClick;
        return item;
    }

    private void RefreshStatus()
    {
        var channel = watcher.Current;
        var connectionState = obsUpdater.ConnectionState;

        var channelText = channel switch
        {
            null => Strings.TrayChannelNotInVoice,
            { Name: { Length: > 0 } name } => name,
            _ => Strings.TrayChannelUnknown,
        };

        var obsText = connectionState switch
        {
            ConnectionState.Connected => Strings.TrayObsConnected,
            ConnectionState.Connecting => Strings.TrayObsConnecting,
            ConnectionState.Disconnected => Strings.TrayObsDisconnected,
            _ => connectionState.ToString(),
        };

        channelStatusItem.Header = Strings.TrayChannelFormat(channelText);
        obsStatusItem.Header = Strings.TrayObsFormat(obsText);

        var tooltip = Strings.TrayTooltipFormat(channelText, obsText);
        trayIcon.ToolTipText = tooltip.Length > TooltipMaxLength
            ? tooltip[..(TooltipMaxLength - 1)] + "..."
            : tooltip;
    }

    private void OnSettingsClicked(object? sender, RoutedEventArgs e) => ShowSettings(onOverlayTab: false);

    /// <summary>
    /// Same window as Settings, opened on the overlay tab. Appearance is the
    /// thing you reopen to tweak, so it gets its own way in — but not its own
    /// window, which would mean a second Save with different semantics.
    /// </summary>
    private void OnOverlayAppearanceClicked(object? sender, RoutedEventArgs e) => ShowSettings(onOverlayTab: true);

    private void ShowSettings(bool onOverlayTab)
    {
        // A tray app has no owner window to be modal against, and ShowDialog on
        // a repeat click would deadlock behind the first one. Keep a single
        // instance and raise it instead.
        if (settingsWindow is { IsLoaded: true })
        {
            if (onOverlayTab) settingsWindow.SelectOverlayTab();
            settingsWindow.Activate();
            return;
        }

        settingsWindow = new SettingsWindow(
            session,
            obsOptions.CurrentValue,
            autoStart,
            obsTester,
            () => overlayOptions.CurrentValue,
            RefreshOverlayAsync);

        settingsWindow.Closed += (_, _) =>
        {
            var signedOut = settingsWindow?.SignedOut ?? false;
            settingsWindow = null;
            if (signedOut)
            {
                // Exit so the user gets the setup wizard on next launch.
                logger.LogInformation("Sign-out from settings; exiting application");
                lifetime.StopApplication();
            }
        };

        if (onOverlayTab) settingsWindow.SelectOverlayTab();
        settingsWindow.Show();
        settingsWindow.Activate();
    }

    /// <summary>
    /// Pick up an appearance change and push it. The reload is not optional:
    /// settings.json is watched, but that watch is asynchronous, so re-pushing
    /// straight after a save would otherwise send the values it just replaced.
    /// </summary>
    private async Task RefreshOverlayAsync(CancellationToken cancellationToken)
    {
        (configuration as IConfigurationRoot)?.Reload();
        await obsUpdater.RefreshAsync(cancellationToken).ConfigureAwait(true);
    }

    private void OnOpenLogFolderClicked(object? sender, RoutedEventArgs e)
    {
        var path = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DiscordOverlay",
            "logs");
        try
        {
            Directory.CreateDirectory(path);
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true,
            });
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to open log folder at {Path}", path);
        }
    }

    private async void OnCheckForUpdatesClicked(object? sender, RoutedEventArgs e)
    {
        if (!updater.IsInstalled)
        {
            await ShowMessageAsync(Strings.AppName, Strings.UpdatesNotInstalledMessage).ConfigureAwait(true);
            return;
        }

        trayIcon.ShowNotification(Strings.AppName, Strings.UpdatesCheckingBalloon, NotificationIcon.Info);
        try
        {
            var update = await updater.CheckForUpdatesAsync().ConfigureAwait(true);
            if (update is null)
            {
                trayIcon.ShowNotification(
                    Strings.AppName,
                    Strings.UpdatesUpToDateBalloon(updater.CurrentVersion ?? "?"),
                    NotificationIcon.Info);
                return;
            }

            var confirmed = await ConfirmAsync(
                Strings.UpdatesAvailableTitle,
                Strings.UpdatesAvailablePrompt(update.TargetFullRelease.Version.ToString())).ConfigureAwait(true);
            if (confirmed)
            {
                await updater.DownloadAndApplyAsync(update).ConfigureAwait(true);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Update check from tray failed");
            await ShowMessageAsync(Strings.AppName, Strings.UpdatesCheckFailed(ex.Message)).ConfigureAwait(true);
        }
    }

    private static async Task ShowMessageAsync(string title, string content)
    {
        var box = new MessageBox
        {
            Title = title,
            Content = content,
            CloseButtonText = Strings.DialogClose,
        };
        await box.ShowDialogAsync().ConfigureAwait(true);
    }

    private static async Task<bool> ConfirmAsync(string title, string content)
    {
        var box = new MessageBox
        {
            Title = title,
            Content = content,
            PrimaryButtonText = Strings.DialogYes,
            CloseButtonText = Strings.DialogNo,
        };
        var result = await box.ShowDialogAsync().ConfigureAwait(true);
        return result == Wpf.Ui.Controls.MessageBoxResult.Primary;
    }

    private void OnQuitClicked(object? sender, RoutedEventArgs e)
    {
        logger.LogInformation("Quit requested from tray menu");
        lifetime.StopApplication();
    }

    private void OnHostStopping()
    {
        logger.LogInformation("Host stopping; shutting down the WPF dispatcher");
        System.Windows.Application.Current?.Dispatcher.Invoke(
            () => System.Windows.Application.Current.Shutdown());
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        statusTimer.Stop();
        trayIcon.Dispose();
    }
}
