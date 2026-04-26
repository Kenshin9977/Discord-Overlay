using System.Text.Json.Serialization;

namespace DiscordOverlay.Core.Auth;

public sealed class DiscordTokenResponse
{
    [JsonPropertyName("access_token")] public required string AccessToken { get; init; }

    [JsonPropertyName("token_type")] public string? TokenType { get; init; }

    [JsonPropertyName("expires_in")] public int ExpiresIn { get; init; }

    [JsonPropertyName("refresh_token")] public string? RefreshToken { get; init; }

    [JsonPropertyName("scope")] public string? Scope { get; init; }

    public DateTimeOffset GetExpiresAt(DateTimeOffset now) => now + TimeSpan.FromSeconds(ExpiresIn);

    public override string ToString()
        => $"DiscordTokenResponse {{ AccessToken = ***, ExpiresIn = {ExpiresIn}, Scope = {Scope} }}";
}
