namespace DiscordOverlay.Core.Discord;

public sealed class DiscordVoiceChannelInfo : IEquatable<DiscordVoiceChannelInfo>
{
    public required string ChannelId { get; init; }

    public string? GuildId { get; init; }

    public string? Name { get; init; }

    public bool Equals(DiscordVoiceChannelInfo? other)
        => other is not null && ChannelId == other.ChannelId && GuildId == other.GuildId;

    public override bool Equals(object? obj) => Equals(obj as DiscordVoiceChannelInfo);

    public override int GetHashCode() => HashCode.Combine(ChannelId, GuildId);

    public override string ToString() => $"{Name ?? "?"} (channel={ChannelId}, guild={GuildId ?? "?"})";
}
