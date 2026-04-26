namespace DiscordOverlay.Core.Auth;

public interface IDiscordTokenExchange
{
    Task<DiscordTokenResponse> ExchangeAuthorizationCodeAsync(
        DiscordOAuthCredentials credentials,
        string authorizationCode,
        CancellationToken cancellationToken = default);

    Task<DiscordTokenResponse> RefreshAsync(
        DiscordOAuthCredentials credentials,
        string refreshToken,
        CancellationToken cancellationToken = default);
}
