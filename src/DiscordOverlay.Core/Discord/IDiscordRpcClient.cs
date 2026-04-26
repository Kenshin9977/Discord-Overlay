namespace DiscordOverlay.Core.Discord;

public interface IDiscordRpcClient : IAsyncDisposable
{
    event EventHandler<DiscordVoiceChannelSelectedEventArgs>? VoiceChannelSelected;

    event EventHandler? Disconnected;

    bool IsConnected { get; }

    Task ConnectAsync(string clientId, CancellationToken cancellationToken = default);

    Task<string> AuthorizeAsync(IReadOnlyList<string> scopes, CancellationToken cancellationToken = default);

    Task<DiscordAuthenticateResult> AuthenticateAsync(string accessToken, CancellationToken cancellationToken = default);

    Task SubscribeVoiceChannelSelectAsync(CancellationToken cancellationToken = default);

    Task DisconnectAsync(CancellationToken cancellationToken = default);
}
