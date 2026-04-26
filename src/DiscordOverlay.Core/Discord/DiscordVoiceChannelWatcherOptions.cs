namespace DiscordOverlay.Core.Discord;

public sealed class DiscordVoiceChannelWatcherOptions
{
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(5);

    public TimeSpan RefreshTimeout { get; set; } = TimeSpan.FromSeconds(5);
}
