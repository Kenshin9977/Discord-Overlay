using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DiscordOverlay.Core.Discord;

public sealed class DiscordVoiceChannelWatcher : BackgroundService, IDiscordVoiceChannelWatcher
{
    private readonly IDiscordRpcClient client;
    private readonly ILogger<DiscordVoiceChannelWatcher> logger;
    private readonly DiscordVoiceChannelWatcherOptions options;
    private readonly SemaphoreSlim refreshLock = new(1, 1);

    private DiscordVoiceChannelInfo? current;

    public DiscordVoiceChannelWatcher(
        IDiscordRpcClient client,
        IOptions<DiscordVoiceChannelWatcherOptions> options,
        ILogger<DiscordVoiceChannelWatcher> logger)
    {
        this.client = client;
        this.options = options.Value;
        this.logger = logger;

        client.VoiceChannelSelected += OnVoiceChannelSelected;
        client.Disconnected += OnDisconnected;
    }

    public DiscordVoiceChannelInfo? Current => current;

    public event EventHandler<DiscordVoiceChannelInfo?>? Changed;

    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        if (!client.IsConnected)
        {
            return;
        }

        if (!await refreshLock.WaitAsync(0, cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(options.RefreshTimeout);

            var channel = await client.GetSelectedVoiceChannelAsync(cts.Token).ConfigureAwait(false);
            var info = channel is null
                ? null
                : new DiscordVoiceChannelInfo
                {
                    ChannelId = channel.Id,
                    GuildId = channel.GuildId,
                    Name = channel.Name,
                };

            SetCurrent(info);
        }
        catch (OperationCanceledException)
        {
            // honored cancellation, nothing to log
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Voice channel refresh failed (will retry)");
        }
        finally
        {
            refreshLock.Release();
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await RefreshAsync(stoppingToken).ConfigureAwait(false);
                try
                {
                    await Task.Delay(options.PollInterval, stoppingToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
        finally
        {
            client.VoiceChannelSelected -= OnVoiceChannelSelected;
            client.Disconnected -= OnDisconnected;
        }
    }

    private async void OnVoiceChannelSelected(object? sender, DiscordVoiceChannelSelectedEventArgs e)
    {
        try
        {
            // The event payload from Discord is treated as a hint; the source of truth
            // is GET_SELECTED_VOICE_CHANNEL. This also covers forced moves where the
            // event itself is unreliable.
            await RefreshAsync(CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Refresh on VOICE_CHANNEL_SELECT failed");
        }
    }

    private void OnDisconnected(object? sender, EventArgs e) => SetCurrent(null);

    private void SetCurrent(DiscordVoiceChannelInfo? info)
    {
        if (Equals(current, info))
        {
            return;
        }

        current = info;
        logger.LogInformation("Voice channel is now {Channel}", info?.ToString() ?? "<none>");

        try
        {
            Changed?.Invoke(this, info);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Voice channel Changed handler threw");
        }
    }
}
