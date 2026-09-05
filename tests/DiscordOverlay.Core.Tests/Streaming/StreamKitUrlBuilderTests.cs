using DiscordOverlay.Core.Discord;
using DiscordOverlay.Core.Streaming;
using Microsoft.Extensions.Options;

namespace DiscordOverlay.Core.Tests.Streaming;

public class StreamKitUrlBuilderTests
{
    [Fact]
    public void Build_ReturnsNull_WhenChannelIsNull()
    {
        var sut = BuildSut(new StreamKitOverlayOptions());
        Assert.Null(sut.Build(null));
    }

    [Fact]
    public void Build_ReturnsNull_WhenGuildIdMissing()
    {
        var sut = BuildSut(new StreamKitOverlayOptions());
        var info = new DiscordVoiceChannelInfo { ChannelId = "C1", GuildId = null };
        Assert.Null(sut.Build(info));
    }

    [Fact]
    public void Build_AssemblesStreamKitUrl_WithDefaultOptions()
    {
        var sut = BuildSut(new StreamKitOverlayOptions());
        var info = new DiscordVoiceChannelInfo { ChannelId = "111222333", GuildId = "999888777", Name = "general" };

        var url = sut.Build(info)!;

        Assert.StartsWith("https://streamkit.discord.com/overlay/voice/999888777/111222333?", url);
        Assert.Contains("text_color=%23ffffff", url);
        Assert.Contains("text_size=14", url);
        Assert.Contains("text_outline_size=0", url);
        Assert.Contains("text_shadow_color=%23000000", url);
        Assert.Contains("text_shadow_size=0", url);
        Assert.Contains("bg_opacity=0&", url);
        Assert.Contains("bg_shadow_color=%23000000", url);
        Assert.Contains("bg_shadow_size=0", url);
        Assert.Contains("limit_speaking=false", url);
        Assert.Contains("small_avatars=false", url);
        Assert.Contains("hide_names=false", url);
        Assert.Contains("streamer_avatar_first=false", url);
    }

    [Fact]
    public void Build_EmitsExactlyTheParametersTheVoiceWidgetReads()
    {
        // The voice widget's own settings object is the contract. icon, online
        // and logo belong to the status widget and are ignored here — sending
        // them is what made them look like settings that did not work.
        var sut = BuildSut(new StreamKitOverlayOptions());
        var info = new DiscordVoiceChannelInfo { ChannelId = "C", GuildId = "G" };

        var query = sut.Build(info)!.Split('?')[1].Split('&')
            .Select(pair => pair.Split('=')[0])
            .ToArray();

        Assert.Equal(
            [
                "text_color", "text_size",
                "text_outline_color", "text_outline_size",
                "text_shadow_color", "text_shadow_size",
                "bg_color", "bg_opacity",
                "bg_shadow_color", "bg_shadow_size",
                "limit_speaking", "small_avatars", "hide_names", "streamer_avatar_first",
            ],
            query);
    }

    [Fact]
    public void Build_RespectsOptionOverrides()
    {
        var sut = BuildSut(new StreamKitOverlayOptions
        {
            TextSize = 18,
            TextShadowColor = "#ff0000",
            TextShadowSize = 3,
            BackgroundOpacity = 0.5,
            BackgroundShadowColor = "#00ff00",
            BackgroundShadowSize = 6,
            LimitSpeaking = true,
            HideNames = true,
            StreamerAvatarFirst = true,
        });
        var info = new DiscordVoiceChannelInfo { ChannelId = "C", GuildId = "G" };

        var url = sut.Build(info)!;

        Assert.Contains("text_size=18", url);
        Assert.Contains("text_shadow_color=%23ff0000", url);
        Assert.Contains("text_shadow_size=3", url);
        Assert.Contains("bg_opacity=0.5", url);
        Assert.Contains("bg_shadow_color=%2300ff00", url);
        Assert.Contains("bg_shadow_size=6", url);
        Assert.Contains("limit_speaking=true", url);
        Assert.Contains("hide_names=true", url);
        Assert.Contains("streamer_avatar_first=true", url);
    }

    [Theory]
    // The scale users actually write, and the one StreamKit itself uses.
    [InlineData(0, "0")]
    [InlineData(0.75, "0.75")]
    [InlineData(1, "1")]
    // Above 1 is a settings.json from when this was a 0-100 percentage.
    [InlineData(50, "0.5")]
    [InlineData(95, "0.95")]
    // Nonsense in either reading still has to produce a value StreamKit accepts.
    [InlineData(150, "1")]
    [InlineData(-3, "0")]
    public void Build_TreatsBackgroundOpacityAsAFraction(double configured, string expected)
    {
        var sut = BuildSut(new StreamKitOverlayOptions { BackgroundOpacity = configured });
        var info = new DiscordVoiceChannelInfo { ChannelId = "C", GuildId = "G" };

        Assert.Contains($"bg_opacity={expected}&", sut.Build(info)!);
    }

    [Fact]
    public void Build_UrlEncodesColorHashes()
    {
        var sut = BuildSut(new StreamKitOverlayOptions
        {
            TextColor = "#ff00aa",
            BackgroundColor = "#112233",
        });
        var info = new DiscordVoiceChannelInfo { ChannelId = "C", GuildId = "G" };

        var url = sut.Build(info)!;

        Assert.Contains("text_color=%23ff00aa", url);
        Assert.Contains("bg_color=%23112233", url);
    }

    private static StreamKitUrlBuilder BuildSut(StreamKitOverlayOptions options)
    {
        var monitor = new TestOptionsMonitor<StreamKitOverlayOptions>(options);
        return new StreamKitUrlBuilder(monitor);
    }

    private sealed class TestOptionsMonitor<T>(T value) : IOptionsMonitor<T>
    {
        public T CurrentValue { get; } = value;
        public T Get(string? name) => CurrentValue;
        public IDisposable? OnChange(Action<T, string?> listener) => null;
    }
}
