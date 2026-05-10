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

    // Time the WebSocket handshake + AUTHENTICATE takes from ConnectAsync
    // to ConnectionState == Connected. ObsClient resolves ConnectAsync
    // before authentication completes, so we poll the state ourselves.
    private static readonly TimeSpan HandshakeTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan HealthCheckInterval = TimeSpan.FromSeconds(15);

    private readonly IDiscordVoiceChannelWatcher watcher;
    private readonly StreamKitUrlBuilder urlBuilder;
    private readonly ILogger<ObsBrowserSourceUpdater> logger;
    private readonly IOptionsMonitor<ObsConnectionOptions> options;
    private readonly SemaphoreSlim pushLock = new(1, 1);

    private volatile int connectionStateRaw = (int)ConnectionState.Disconnected;

    public ObsBrowserSourceUpdater(
        IDiscordVoiceChannelWatcher watcher,
        StreamKitUrlBuilder urlBuilder,
        IOptionsMonitor<ObsConnectionOptions> options,
        ILogger<ObsBrowserSourceUpdater> logger)
    {
        this.watcher = watcher;
        this.urlBuilder = urlBuilder;
        this.options = options;
        this.logger = logger;
    }

    public bool IsConnected => ConnectionState == ConnectionState.Connected;

    public ConnectionState ConnectionState => (ConnectionState)connectionStateRaw;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        watcher.Changed += OnVoiceChannelChanged;

        try
        {
            // Connect-per-push avoids a known issue in OBSClient v3 where
            // ConnectionState stays "Connected" after the underlying socket
            // drops (no PropertyChanged fires, AutoReconnect doesn't kick
            // in). We instead create a fresh connection for every push and
            // for each periodic health probe, so we never trust a stale
            // state machine. See issue #6.
            await PushIfChannelKnownAsync(stoppingToken).ConfigureAwait(false);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(HealthCheckInterval, stoppingToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }

                await ProbeConnectionAsync(stoppingToken).ConfigureAwait(false);
            }
        }
        finally
        {
            watcher.Changed -= OnVoiceChannelChanged;
            connectionStateRaw = (int)ConnectionState.Disconnected;
        }
    }

    private async Task PushIfChannelKnownAsync(CancellationToken cancellationToken)
    {
        var snapshot = watcher.Current;
        if (snapshot is null)
        {
            // No voice channel known yet; the health probe below will keep
            // the tray status accurate until the first VOICE_CHANNEL_SELECT.
            await ProbeConnectionAsync(cancellationToken).ConfigureAwait(false);
            return;
        }

        await PushAsync(snapshot, cancellationToken).ConfigureAwait(false);
    }

    private async void OnVoiceChannelChanged(object? sender, DiscordVoiceChannelInfo? info)
    {
        try
        {
            await PushAsync(info, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to push voice channel update to OBS.");
        }
    }

    private async Task PushAsync(DiscordVoiceChannelInfo? info, CancellationToken cancellationToken)
    {
        var opts = options.CurrentValue;

        if (string.IsNullOrWhiteSpace(opts.Hostname) || string.IsNullOrWhiteSpace(opts.BrowserSourceName))
        {
            logger.LogDebug("OBS connection or BrowserSourceName not configured; skipping push.");
            return;
        }

        var url = urlBuilder.Build(info) ?? BlankUrl;

        await pushLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await using var session = await OpenSessionAsync(opts, cancellationToken).ConfigureAwait(false);
            if (session is null)
            {
                logger.LogWarning(
                    "OBS push skipped: could not connect to {Host}:{Port}. The overlay URL will be retried on the next voice-channel change.",
                    opts.Hostname, opts.Port);
                return;
            }

            try
            {
                await session.Client.SetInputSettings(
                    inputName: opts.BrowserSourceName,
                    inputSettings: new Dictionary<string, object> { ["url"] = url },
                    overlay: true).ConfigureAwait(false);

                logger.LogInformation(
                    "Pushed StreamKit URL to OBS Browser Source '{Source}': {Url}",
                    opts.BrowserSourceName, url);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex,
                    "OBS SetInputSettings failed for source '{Source}'. " +
                    "Verify a Browser Source named exactly '{Source}' exists in your current OBS scene collection.",
                    opts.BrowserSourceName, opts.BrowserSourceName);
            }
        }
        finally
        {
            pushLock.Release();
        }
    }

    private async Task ProbeConnectionAsync(CancellationToken cancellationToken)
    {
        var opts = options.CurrentValue;
        if (string.IsNullOrWhiteSpace(opts.Hostname))
        {
            connectionStateRaw = (int)ConnectionState.Disconnected;
            return;
        }

        // The probe just verifies reachability for the tray status — we
        // open, observe the handshake reach Connected, then close. The
        // connection-state field reflects "OBS was reachable on the last
        // probe/push", not "we have an open socket right now".
        await using var session = await OpenSessionAsync(opts, cancellationToken).ConfigureAwait(false);
        _ = session;
    }

    private async Task<ObsSession?> OpenSessionAsync(ObsConnectionOptions opts, CancellationToken cancellationToken)
    {
        var client = new ObsClient();

        try
        {
            await client.ConnectAsync(
                autoReconnect: false,
                password: opts.Password,
                hostname: opts.Hostname,
                port: opts.Port,
                eventSubscription: EventSubscriptions.None).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "OBS ConnectAsync threw");
            try { client.Disconnect(); } catch { /* best effort */ }
            client.Dispose();
            connectionStateRaw = (int)ConnectionState.Disconnected;
            return null;
        }

        // ConnectAsync returns once the WebSocket is open, but the
        // Hello -> Identify -> Identified handshake may still be in flight.
        // Poll until we land in Connected, or fail out.
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(HandshakeTimeout);
        try
        {
            while (!cts.IsCancellationRequested)
            {
                var state = client.ConnectionState;
                if (state == ConnectionState.Connected)
                {
                    connectionStateRaw = (int)ConnectionState.Connected;
                    return new ObsSession(client);
                }
                if (state == ConnectionState.Disconnected)
                {
                    break;
                }

                await Task.Delay(50, cts.Token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // Handshake timeout — fall through to cleanup.
        }

        try { client.Disconnect(); } catch { /* best effort */ }
        client.Dispose();
        connectionStateRaw = (int)ConnectionState.Disconnected;
        return null;
    }

    private sealed class ObsSession(ObsClient client) : IAsyncDisposable
    {
        public ObsClient Client { get; } = client;

        public ValueTask DisposeAsync()
        {
            // Closing this short-lived socket is intentional and does not
            // mean OBS is unreachable, so we leave connectionStateRaw alone
            // — the next probe/push will refresh it if reachability changes.
            try { Client.Disconnect(); } catch { /* best effort */ }
            Client.Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
