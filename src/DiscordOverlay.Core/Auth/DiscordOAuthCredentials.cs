namespace DiscordOverlay.Core.Auth;

public sealed record DiscordOAuthCredentials(string ClientId, string ClientSecret, string RedirectUri)
{
    public const string DefaultRedirectUri = "http://localhost:3000/callback";

    public override string ToString()
        => $"DiscordOAuthCredentials {{ ClientId = {ClientId}, ClientSecret = ***, RedirectUri = {RedirectUri} }}";
}
