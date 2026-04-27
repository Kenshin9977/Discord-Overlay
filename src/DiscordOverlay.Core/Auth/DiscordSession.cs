using DiscordOverlay.Core.Discord;
using Microsoft.Extensions.Logging;

namespace DiscordOverlay.Core.Auth;

public sealed class DiscordSession : IDiscordSession
{
    private static readonly IReadOnlyList<string> RequiredScopes = new[] { "rpc", "identify" };
    private static readonly TimeSpan TokenRefreshSkew = TimeSpan.FromMinutes(5);

    private readonly IDiscordRpcClient rpcClient;
    private readonly IDiscordTokenExchange tokenExchange;
    private readonly IDiscordCredentialStore credentialStore;
    private readonly ILogger<DiscordSession> logger;
    private readonly TimeProvider timeProvider;

    public DiscordSession(
        IDiscordRpcClient rpcClient,
        IDiscordTokenExchange tokenExchange,
        IDiscordCredentialStore credentialStore,
        ILogger<DiscordSession> logger,
        TimeProvider? timeProvider = null)
    {
        this.rpcClient = rpcClient;
        this.tokenExchange = tokenExchange;
        this.credentialStore = credentialStore;
        this.logger = logger;
        this.timeProvider = timeProvider ?? TimeProvider.System;
    }

    public DiscordCredentialBundle? Current { get; private set; }

    public async Task<DiscordCredentialBundle> SetupAsync(
        string clientId,
        string clientSecret,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clientId);
        ArgumentException.ThrowIfNullOrWhiteSpace(clientSecret);

        var credentials = new DiscordOAuthCredentials(
            clientId.Trim(),
            clientSecret.Trim(),
            DiscordOAuthCredentials.DefaultRedirectUri);

        logger.LogInformation("Starting Discord setup flow for client {ClientId}", credentials.ClientId);

        if (rpcClient.IsConnected)
        {
            await rpcClient.DisconnectAsync(cancellationToken).ConfigureAwait(false);
        }

        await rpcClient.ConnectAsync(credentials.ClientId, cancellationToken).ConfigureAwait(false);

        // Discord shows the consent popup inside its own client during AUTHORIZE.
        var code = await rpcClient.AuthorizeAsync(RequiredScopes, cancellationToken).ConfigureAwait(false);

        var token = await tokenExchange
            .ExchangeAuthorizationCodeAsync(credentials, code, cancellationToken)
            .ConfigureAwait(false);

        await rpcClient.AuthenticateAsync(token.AccessToken, cancellationToken).ConfigureAwait(false);
        await rpcClient.SubscribeVoiceChannelSelectAsync(cancellationToken).ConfigureAwait(false);

        var bundle = new DiscordCredentialBundle(
            ClientId: credentials.ClientId,
            ClientSecret: credentials.ClientSecret,
            RedirectUri: credentials.RedirectUri,
            AccessToken: token.AccessToken,
            RefreshToken: token.RefreshToken,
            ExpiresAt: token.GetExpiresAt(timeProvider.GetUtcNow()));

        await credentialStore.SaveAsync(bundle, cancellationToken).ConfigureAwait(false);
        Current = bundle;
        logger.LogInformation("Discord setup completed and persisted to credential store");
        return bundle;
    }

    public async Task<bool> ResumeFromStoreAsync(CancellationToken cancellationToken = default)
    {
        var bundle = await credentialStore.LoadAsync(cancellationToken).ConfigureAwait(false);
        if (bundle is null)
        {
            logger.LogInformation("No stored credentials — initial setup required.");
            return false;
        }

        try
        {
            await rpcClient.ConnectAsync(bundle.ClientId, cancellationToken).ConfigureAwait(false);

            if (bundle.IsTokenExpired(TokenRefreshSkew, timeProvider.GetUtcNow()))
            {
                logger.LogInformation("Stored access token expired or near expiry; refreshing.");
                if (string.IsNullOrEmpty(bundle.RefreshToken))
                {
                    logger.LogWarning("No refresh token available; user must re-run setup.");
                    return false;
                }

                var refreshed = await tokenExchange
                    .RefreshAsync(bundle.AsOAuthCredentials(), bundle.RefreshToken, cancellationToken)
                    .ConfigureAwait(false);

                bundle = bundle with
                {
                    AccessToken = refreshed.AccessToken,
                    RefreshToken = refreshed.RefreshToken ?? bundle.RefreshToken,
                    ExpiresAt = refreshed.GetExpiresAt(timeProvider.GetUtcNow()),
                };
                await credentialStore.SaveAsync(bundle, cancellationToken).ConfigureAwait(false);
            }

            await rpcClient.AuthenticateAsync(bundle.AccessToken!, cancellationToken).ConfigureAwait(false);
            await rpcClient.SubscribeVoiceChannelSelectAsync(cancellationToken).ConfigureAwait(false);

            Current = bundle;
            logger.LogInformation("Discord session resumed from stored credentials.");
            return true;
        }
        catch (DiscordOAuthException ex)
        {
            logger.LogWarning(ex, "OAuth refresh failed; user must re-run setup.");
            return false;
        }
    }

    public async Task SignOutAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (rpcClient.IsConnected)
            {
                await rpcClient.DisconnectAsync(cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            await credentialStore.ClearAsync(cancellationToken).ConfigureAwait(false);
            Current = null;
            logger.LogInformation("Discord session signed out and credentials cleared.");
        }
    }
}
