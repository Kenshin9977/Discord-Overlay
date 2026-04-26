using DiscordOverlay.Core.Discord;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace DiscordOverlay.Core.Tests.Discord;

public class DiscordVoiceChannelWatcherTests
{
    private readonly FakeDiscordRpcClient client = new();
    private readonly DiscordVoiceChannelWatcher sut;
    private readonly List<DiscordVoiceChannelInfo?> changes = [];

    public DiscordVoiceChannelWatcherTests()
    {
        var options = Options.Create(new DiscordVoiceChannelWatcherOptions());
        sut = new DiscordVoiceChannelWatcher(client, options, NullLogger<DiscordVoiceChannelWatcher>.Instance);
        sut.Changed += (_, info) => changes.Add(info);
    }

    [Fact]
    public async Task RefreshAsync_WhenNotConnected_DoesNothing()
    {
        client.IsConnected = false;
        client.CurrentChannel = new DiscordRpcVoiceChannel { Id = "C1", GuildId = "G1" };

        await sut.RefreshAsync();

        Assert.Null(sut.Current);
        Assert.Empty(changes);
    }

    [Fact]
    public async Task RefreshAsync_WhenConnected_PopulatesCurrent_AndFiresChanged()
    {
        client.CurrentChannel = new DiscordRpcVoiceChannel { Id = "C1", GuildId = "G1", Name = "general" };

        await sut.RefreshAsync();

        Assert.NotNull(sut.Current);
        Assert.Equal("C1", sut.Current!.ChannelId);
        Assert.Equal("G1", sut.Current.GuildId);
        Assert.Equal("general", sut.Current.Name);
        Assert.Single(changes);
    }

    [Fact]
    public async Task RefreshAsync_SameChannel_DoesNotFireAgain()
    {
        client.CurrentChannel = new DiscordRpcVoiceChannel { Id = "C1", GuildId = "G1" };

        await sut.RefreshAsync();
        await sut.RefreshAsync();

        Assert.Single(changes);
    }

    [Fact]
    public async Task RefreshAsync_ChannelChange_FiresAgain()
    {
        client.CurrentChannel = new DiscordRpcVoiceChannel { Id = "C1", GuildId = "G1" };
        await sut.RefreshAsync();

        client.CurrentChannel = new DiscordRpcVoiceChannel { Id = "C2", GuildId = "G2" };
        await sut.RefreshAsync();

        Assert.Equal(2, changes.Count);
        Assert.Equal("C2", changes[1]!.ChannelId);
    }

    [Fact]
    public async Task ForcedMove_DetectedByPolling_WhenEventDoesNotFire()
    {
        // Initial state: in channel C1.
        client.CurrentChannel = new DiscordRpcVoiceChannel { Id = "C1", GuildId = "G1" };
        await sut.RefreshAsync();
        Assert.Single(changes);

        // Simulate a moderator dragging us into C2. Discord does NOT fire
        // VOICE_CHANNEL_SELECT in this case (the bug voice-channel-grabber
        // hits). Polling alone is the safety net that catches the move.
        client.CurrentChannel = new DiscordRpcVoiceChannel { Id = "C2", GuildId = "G1" };
        await sut.RefreshAsync();

        Assert.Equal(2, changes.Count);
        Assert.Equal("C2", sut.Current!.ChannelId);
    }

    [Fact]
    public async Task LeavingVoice_FiresChanged_WithNull()
    {
        client.CurrentChannel = new DiscordRpcVoiceChannel { Id = "C1", GuildId = "G1" };
        await sut.RefreshAsync();

        client.CurrentChannel = null;
        await sut.RefreshAsync();

        Assert.Equal(2, changes.Count);
        Assert.Null(changes[1]);
        Assert.Null(sut.Current);
    }

    [Fact]
    public async Task Disconnected_ClearsCurrent_AndFiresChanged()
    {
        client.CurrentChannel = new DiscordRpcVoiceChannel { Id = "C1", GuildId = "G1" };
        await sut.RefreshAsync();
        Assert.Single(changes);

        client.RaiseDisconnected();

        Assert.Equal(2, changes.Count);
        Assert.Null(changes[1]);
        Assert.Null(sut.Current);
    }

    private sealed class FakeDiscordRpcClient : IDiscordRpcClient
    {
        public bool IsConnected { get; set; } = true;

        public DiscordRpcVoiceChannel? CurrentChannel { get; set; }

        public event EventHandler<DiscordVoiceChannelSelectedEventArgs>? VoiceChannelSelected;

        public event EventHandler? Disconnected;

        public void RaiseDisconnected() => Disconnected?.Invoke(this, EventArgs.Empty);

        public void RaiseVoiceChannelSelected(string? channelId, string? guildId)
            => VoiceChannelSelected?.Invoke(this,
                new DiscordVoiceChannelSelectedEventArgs { ChannelId = channelId, GuildId = guildId });

        public Task<DiscordRpcVoiceChannel?> GetSelectedVoiceChannelAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(CurrentChannel);

        public Task ConnectAsync(string clientId, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<string> AuthorizeAsync(IReadOnlyList<string> scopes, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<DiscordAuthenticateResult> AuthenticateAsync(string accessToken, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task SubscribeVoiceChannelSelectAsync(CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task DisconnectAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
