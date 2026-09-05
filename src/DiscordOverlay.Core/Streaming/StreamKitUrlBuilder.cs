using System.Globalization;
using DiscordOverlay.Core.Discord;
using Microsoft.Extensions.Options;

namespace DiscordOverlay.Core.Streaming;

public sealed class StreamKitUrlBuilder(IOptionsMonitor<StreamKitOverlayOptions> options)
{
    public const string BaseUrl = "https://streamkit.discord.com/overlay/voice";

    public string? Build(DiscordVoiceChannelInfo? channel)
    {
        if (channel is null) return null;
        if (string.IsNullOrEmpty(channel.GuildId) || string.IsNullOrEmpty(channel.ChannelId))
        {
            return null;
        }

        var opts = options.CurrentValue;
        var query = new List<string>
        {
            $"icon={LowerBool(opts.ShowIcon)}",
            $"online={LowerBool(opts.OnlineOnly)}",
            $"logo={Uri.EscapeDataString(opts.Logo)}",
            $"text_color={Uri.EscapeDataString(opts.TextColor)}",
            $"text_size={opts.TextSize.ToString(CultureInfo.InvariantCulture)}",
            $"text_outline_color={Uri.EscapeDataString(opts.TextOutlineColor)}",
            $"text_outline_size={opts.TextOutlineSize.ToString(CultureInfo.InvariantCulture)}",
            $"bg_color={Uri.EscapeDataString(opts.BackgroundColor)}",
            $"bg_opacity={NormalizeOpacity(opts.BackgroundOpacity).ToString("0.##", CultureInfo.InvariantCulture)}",
            $"limit_speaking={LowerBool(opts.LimitSpeaking)}",
            $"small_avatars={LowerBool(opts.SmallAvatars)}",
            $"hide_names={LowerBool(opts.HideNames)}",
            $"streamer_avatar_first={LowerBool(opts.StreamerAvatarFirst)}",
        };

        return $"{BaseUrl}/{channel.GuildId}/{channel.ChannelId}?{string.Join('&', query)}";
    }

    private static string LowerBool(bool value) => value ? "true" : "false";

    /// <summary>
    /// StreamKit reads <c>bg_opacity</c> as a 0-1 fraction, and so does the
    /// setting, so the common case is a straight pass-through. Anything above 1
    /// is a settings.json written when this was a 0-100 percentage; divide it
    /// down rather than let an old config turn the background fully opaque.
    /// </summary>
    private static double NormalizeOpacity(double value)
    {
        if (double.IsNaN(value)) return 0;
        if (value > 1) value /= 100.0;
        return Math.Clamp(value, 0, 1);
    }
}
