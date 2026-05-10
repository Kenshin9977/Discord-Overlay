using DiscordOverlay.App.Resources;
using DiscordOverlay.App.Settings;
using DiscordOverlay.Core.Auth;
using DiscordOverlay.Core.Discord;
using DiscordOverlay.Core.Streaming;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OBSStudioClient.Enums;

namespace DiscordOverlay.App.Hosting;

public sealed class TrayApplicationContext : ApplicationContext
{
    private const int TooltipMaxLength = 127;
    private const int StatusRefreshIntervalMs = 1000;

    private readonly ILogger<TrayApplicationContext> logger;
    private readonly IHostApplicationLifetime lifetime;
    private readonly IDiscordVoiceChannelWatcher watcher;
    private readonly ObsBrowserSourceUpdater obsUpdater;
    private readonly ObsConnectionTester obsTester;
    private readonly IDiscordSession session;
    private readonly IOptionsMonitor<ObsConnectionOptions> obsOptions;
    private readonly AutoStartManager autoStart;
    private readonly AppUpdater updater;

    private readonly NotifyIcon notifyIcon;
    private readonly System.Windows.Forms.Timer statusTimer;
    private readonly ToolStripMenuItem channelStatusItem;
    private readonly ToolStripMenuItem obsStatusItem;

    public TrayApplicationContext(
        ILogger<TrayApplicationContext> logger,
        IHostApplicationLifetime lifetime,
        IDiscordVoiceChannelWatcher watcher,
        ObsBrowserSourceUpdater obsUpdater,
        ObsConnectionTester obsTester,
        IDiscordSession session,
        IOptionsMonitor<ObsConnectionOptions> obsOptions,
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
        this.autoStart = autoStart;
        this.updater = updater;

        var menu = new ContextMenuStrip();
        channelStatusItem = new ToolStripMenuItem(Strings.TrayChannelStarting) { Enabled = false };
        obsStatusItem = new ToolStripMenuItem(Strings.TrayObsStarting) { Enabled = false };
        menu.Items.Add(channelStatusItem);
        menu.Items.Add(obsStatusItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(Strings.TrayMenuSettings, null, OnSettingsClicked);
        menu.Items.Add(Strings.TrayMenuCheckUpdates, null, OnCheckForUpdatesClicked);
        menu.Items.Add(Strings.TrayMenuOpenLogFolder, null, OnOpenLogFolderClicked);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(Strings.TrayMenuQuit, null, OnQuitClicked);

        notifyIcon = new NotifyIcon
        {
            Icon = LoadApplicationIcon(),
            Text = Strings.TrayInitialTooltip,
            Visible = true,
            ContextMenuStrip = menu,
        };
        notifyIcon.DoubleClick += OnDoubleClicked;

        statusTimer = new System.Windows.Forms.Timer { Interval = StatusRefreshIntervalMs };
        statusTimer.Tick += (_, _) => RefreshStatus();
        statusTimer.Start();
        RefreshStatus();

        lifetime.ApplicationStopping.Register(OnHostStopping);
    }

    private static Icon LoadApplicationIcon()
    {
        try
        {
            return Icon.ExtractAssociatedIcon(Application.ExecutablePath) ?? SystemIcons.Application;
        }
        catch (Exception)
        {
            return SystemIcons.Application;
        }
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

        channelStatusItem.Text = Strings.TrayChannelFormat(channelText);
        obsStatusItem.Text = Strings.TrayObsFormat(obsText);

        var tooltip = Strings.TrayTooltipFormat(channelText, obsText);
        notifyIcon.Text = tooltip.Length > TooltipMaxLength
            ? tooltip[..(TooltipMaxLength - 1)] + "..."
            : tooltip;
    }

    private void OnDoubleClicked(object? sender, EventArgs e) => RefreshStatus();

    private void OnSettingsClicked(object? sender, EventArgs e)
    {
        using var form = new SettingsForm(session, obsOptions.CurrentValue, autoStart, obsTester);
        var result = form.ShowDialog();
        if (result == DialogResult.Abort)
        {
            // Sign-out happened; exit so user gets the wizard on next launch.
            logger.LogInformation("Sign-out from settings; exiting application");
            lifetime.StopApplication();
        }
    }

    private void OnOpenLogFolderClicked(object? sender, EventArgs e)
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

    private async void OnCheckForUpdatesClicked(object? sender, EventArgs e)
    {
        if (!updater.IsInstalled)
        {
            MessageBox.Show(
                Strings.UpdatesNotInstalledMessage,
                Strings.AppName,
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        notifyIcon.ShowBalloonTip(2000, Strings.AppName, Strings.UpdatesCheckingBalloon, ToolTipIcon.Info);
        try
        {
            var update = await updater.CheckForUpdatesAsync().ConfigureAwait(true);
            if (update is null)
            {
                notifyIcon.ShowBalloonTip(3000, Strings.AppName,
                    Strings.UpdatesUpToDateBalloon(updater.CurrentVersion ?? "?"), ToolTipIcon.Info);
                return;
            }

            var prompt = MessageBox.Show(
                Strings.UpdatesAvailablePrompt(update.TargetFullRelease.Version.ToString()),
                Strings.UpdatesAvailableTitle,
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);
            if (prompt == DialogResult.Yes)
            {
                await updater.DownloadAndApplyAsync(update).ConfigureAwait(true);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Update check from tray failed");
            MessageBox.Show(Strings.UpdatesCheckFailed(ex.Message), Strings.AppName,
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void OnQuitClicked(object? sender, EventArgs e)
    {
        logger.LogInformation("Quit requested from tray menu");
        lifetime.StopApplication();
    }

    private void OnHostStopping()
    {
        logger.LogInformation("Host stopping; exiting Windows Forms message loop");
        Application.Exit();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            statusTimer.Stop();
            statusTimer.Dispose();
            notifyIcon.Visible = false;
            notifyIcon.Dispose();
        }
        base.Dispose(disposing);
    }
}
