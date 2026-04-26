namespace DiscordOverlay.Core.Discord;

public interface IDiscordVoiceChannelWatcher
{
    DiscordVoiceChannelInfo? Current { get; }

    event EventHandler<DiscordVoiceChannelInfo?>? Changed;

    Task RefreshAsync(CancellationToken cancellationToken = default);
}
