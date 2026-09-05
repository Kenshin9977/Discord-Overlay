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

        // One entry per setting the voice widget reads, in the order its own
        // defaults object lists them. Sending anything else is not harmless —
        // it is a knob the user can turn that does nothing.
        var query = new List<string>
        {
            $"text_color={Uri.EscapeDataString(opts.TextColor)}",
            $"text_size={Int(opts.TextSize)}",
            $"text_outline_color={Uri.EscapeDataString(opts.TextOutlineColor)}",
            $"text_outline_size={Int(opts.TextOutlineSize)}",
            $"text_shadow_color={Uri.EscapeDataString(opts.TextShadowColor)}",
            $"text_shadow_size={Int(opts.TextShadowSize)}",
            $"bg_color={Uri.EscapeDataString(opts.BackgroundColor)}",
            $"bg_opacity={NormalizeOpacity(opts.BackgroundOpacity).ToString("0.##", CultureInfo.InvariantCulture)}",
            $"bg_shadow_color={Uri.EscapeDataString(opts.BackgroundShadowColor)}",
            $"bg_shadow_size={Int(opts.BackgroundShadowSize)}",
            $"limit_speaking={LowerBool(opts.LimitSpeaking)}",
            $"small_avatars={LowerBool(opts.SmallAvatars)}",
            $"hide_names={LowerBool(opts.HideNames)}",
            $"streamer_avatar_first={LowerBool(opts.StreamerAvatarFirst)}",
        };

        return $"{BaseUrl}/{channel.GuildId}/{channel.ChannelId}?{string.Join('&', query)}";
    }

    private static string LowerBool(bool value) => value ? "true" : "false";

    private static string Int(int value) => value.ToString(CultureInfo.InvariantCulture);

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
