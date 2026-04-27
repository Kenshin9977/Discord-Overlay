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
    private readonly IDiscordSession session;
    private readonly IOptionsMonitor<ObsConnectionOptions> obsOptions;

    private readonly NotifyIcon notifyIcon;
    private readonly System.Windows.Forms.Timer statusTimer;
    private readonly ToolStripMenuItem channelStatusItem;
    private readonly ToolStripMenuItem obsStatusItem;

    public TrayApplicationContext(
        ILogger<TrayApplicationContext> logger,
        IHostApplicationLifetime lifetime,
        IDiscordVoiceChannelWatcher watcher,
        ObsBrowserSourceUpdater obsUpdater,
        IDiscordSession session,
        IOptionsMonitor<ObsConnectionOptions> obsOptions)
    {
        this.logger = logger;
        this.lifetime = lifetime;
        this.watcher = watcher;
        this.obsUpdater = obsUpdater;
        this.session = session;
        this.obsOptions = obsOptions;

        var menu = new ContextMenuStrip();
        channelStatusItem = new ToolStripMenuItem("Channel: starting…") { Enabled = false };
        obsStatusItem = new ToolStripMenuItem("OBS: starting…") { Enabled = false };
        menu.Items.Add(channelStatusItem);
        menu.Items.Add(obsStatusItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Settings…", null, OnSettingsClicked);
        menu.Items.Add("Open log folder", null, OnOpenLogFolderClicked);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Quit", null, OnQuitClicked);

        notifyIcon = new NotifyIcon
        {
            Icon = LoadApplicationIcon(),
            Text = "Discord-Overlay — starting…",
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
            null => "Not in voice",
            { Name: { Length: > 0 } name } => name,
            _ => "(unknown channel)",
        };

        var obsText = connectionState switch
        {
            ConnectionState.Connected => "Connected",
            ConnectionState.Connecting => "Connecting…",
            ConnectionState.Disconnected => "Disconnected",
            _ => connectionState.ToString(),
        };

        channelStatusItem.Text = $"Channel: {channelText}";
        obsStatusItem.Text = $"OBS: {obsText}";

        var tooltip = $"Discord-Overlay — {channelText} • OBS {obsText}";
        notifyIcon.Text = tooltip.Length > TooltipMaxLength
            ? tooltip[..(TooltipMaxLength - 1)] + "…"
            : tooltip;
    }

    private void OnDoubleClicked(object? sender, EventArgs e) => RefreshStatus();

    private void OnSettingsClicked(object? sender, EventArgs e)
    {
        using var form = new SettingsForm(session, obsOptions.CurrentValue);
        var result = form.ShowDialog();
        if (result == DialogResult.Abort)
        {
            // Sign-out happened; exit so user gets the wizard on next launch.
            logger.LogInformation("Sign-out from settings — exiting application");
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

    private void OnQuitClicked(object? sender, EventArgs e)
    {
        logger.LogInformation("Quit requested from tray menu");
        lifetime.StopApplication();
    }

    private void OnHostStopping()
    {
        logger.LogInformation("Host stopping — exiting Windows Forms message loop");
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
