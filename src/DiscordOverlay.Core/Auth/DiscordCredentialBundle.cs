using System.Text.Json.Serialization;

namespace DiscordOverlay.Core.Auth;

public sealed record DiscordCredentialBundle(
    [property: JsonPropertyName("client_id")] string ClientId,
    [property: JsonPropertyName("client_secret")] string ClientSecret,
    [property: JsonPropertyName("redirect_uri")] string RedirectUri,
    [property: JsonPropertyName("access_token")] string? AccessToken,
    [property: JsonPropertyName("refresh_token")] string? RefreshToken,
    [property: JsonPropertyName("expires_at")] DateTimeOffset? ExpiresAt)
{
    public DiscordOAuthCredentials AsOAuthCredentials() => new(ClientId, ClientSecret, RedirectUri);

    public bool IsTokenExpired(TimeSpan skew, DateTimeOffset now)
        => ExpiresAt is null || now + skew >= ExpiresAt.Value;

    public override string ToString()
        => $"DiscordCredentialBundle {{ ClientId = {ClientId}, ClientSecret = ***, RedirectUri = {RedirectUri}, AccessToken = {(AccessToken is null ? "null" : "***")}, RefreshToken = {(RefreshToken is null ? "null" : "***")}, ExpiresAt = {ExpiresAt:O} }}";
}
