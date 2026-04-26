using DiscordOverlay.Core.Discord;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OBSStudioClient;
using OBSStudioClient.Enums;

namespace DiscordOverlay.Core.Streaming;

public sealed class ObsBrowserSourceUpdater : BackgroundService
{
    private const string BlankUrl = "about:blank";

    private readonly IDiscordVoiceChannelWatcher watcher;
    private readonly StreamKitUrlBuilder urlBuilder;
    private readonly ILogger<ObsBrowserSourceUpdater> logger;
    private readonly ObsConnectionOptions options;
    private readonly ObsClient client;

    public ObsBrowserSourceUpdater(
        IDiscordVoiceChannelWatcher watcher,
        StreamKitUrlBuilder urlBuilder,
        IOptions<ObsConnectionOptions> options,
        ILogger<ObsBrowserSourceUpdater> logger)
    {
        this.watcher = watcher;
        this.urlBuilder = urlBuilder;
        this.options = options.Value;
        this.logger = logger;
        this.client = new ObsClient();
    }

    public bool IsConnected => client.ConnectionState == ConnectionState.Connected;

    public ConnectionState ConnectionState => client.ConnectionState;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        watcher.Changed += OnVoiceChannelChanged;
        client.PropertyChanged += OnClientPropertyChanged;

        try
        {
            await client.ConnectAsync(
                autoReconnect: options.AutoReconnect,
                password: options.Password,
                hostname: options.Hostname,
                port: options.Port,
                eventSubscription: EventSubscriptions.None).ConfigureAwait(false);

            logger.LogInformation(
                "OBS WebSocket connection initiated to {Host}:{Port} (autoReconnect={AutoReconnect}); waiting for authentication…",
                options.Hostname, options.Port, options.AutoReconnect);

            try
            {
                await Task.Delay(Timeout.Infinite, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // graceful shutdown
            }
        }
        finally
        {
            watcher.Changed -= OnVoiceChannelChanged;
            client.PropertyChanged -= OnClientPropertyChanged;
            try { client.Disconnect(); } catch { /* best effort */ }
            client.Dispose();
        }
    }

    private async void OnClientPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(ObsClient.ConnectionState)) return;

        if (client.ConnectionState == ConnectionState.Connected)
        {
            logger.LogInformation("OBS WebSocket connected and authenticated.");
            try
            {
                await PushCurrentAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Initial OBS push after connect failed.");
            }
        }
        else
        {
            logger.LogInformation("OBS connection state changed to {State}.", client.ConnectionState);
        }
    }

    private async void OnVoiceChannelChanged(object? sender, DiscordVoiceChannelInfo? info)
    {
        try
        {
            await PushCurrentAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to push voice channel update to OBS.");
        }
    }

    private async Task PushCurrentAsync()
    {
        if (client.ConnectionState != ConnectionState.Connected)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(options.BrowserSourceName))
        {
            logger.LogDebug("BrowserSourceName not configured; skipping OBS push.");
            return;
        }

        var url = urlBuilder.Build(watcher.Current) ?? BlankUrl;

        try
        {
            await client.SetInputSettings(
                inputName: options.BrowserSourceName,
                inputSettings: new Dictionary<string, object> { ["url"] = url },
                overlay: true).ConfigureAwait(false);

            logger.LogInformation(
                "Pushed StreamKit URL to OBS Browser Source '{Source}': {Url}",
                options.BrowserSourceName, url);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "OBS SetInputSettings failed for source '{Source}'. " +
                "Verify the Browser Source exists in OBS and the WebSocket server has permission.",
                options.BrowserSourceName);
        }
    }
}
