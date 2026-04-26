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
        Assert.Contains("icon=true", url);
        Assert.Contains("online=true", url);
        Assert.Contains("logo=white", url);
        Assert.Contains("text_color=%23ffffff", url);
        Assert.Contains("text_size=14", url);
        Assert.Contains("bg_opacity=0", url);
        Assert.Contains("limit_speaking=false", url);
        Assert.Contains("small_avatars=false", url);
        Assert.Contains("hide_names=false", url);
    }

    [Fact]
    public void Build_RespectsOptionOverrides()
    {
        var sut = BuildSut(new StreamKitOverlayOptions
        {
            ShowIcon = false,
            OnlineOnly = false,
            TextSize = 18,
            BackgroundOpacity = 50,
            LimitSpeaking = true,
            HideNames = true,
        });
        var info = new DiscordVoiceChannelInfo { ChannelId = "C", GuildId = "G" };

        var url = sut.Build(info)!;

        Assert.Contains("icon=false", url);
        Assert.Contains("online=false", url);
        Assert.Contains("text_size=18", url);
        Assert.Contains("bg_opacity=0.5", url);
        Assert.Contains("limit_speaking=true", url);
        Assert.Contains("hide_names=true", url);
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
