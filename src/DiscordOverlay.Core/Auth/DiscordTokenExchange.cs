using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace DiscordOverlay.Core.Auth;

public sealed class DiscordTokenExchange(HttpClient httpClient, ILogger<DiscordTokenExchange> logger)
    : IDiscordTokenExchange
{
    public const string TokenEndpoint = "https://discord.com/api/v10/oauth2/token";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public Task<DiscordTokenResponse> ExchangeAuthorizationCodeAsync(
        DiscordOAuthCredentials credentials,
        string authorizationCode,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(credentials);
        ArgumentException.ThrowIfNullOrWhiteSpace(authorizationCode);

        return PostAsync(new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = authorizationCode,
            ["redirect_uri"] = credentials.RedirectUri,
            ["client_id"] = credentials.ClientId,
            ["client_secret"] = credentials.ClientSecret,
        }, cancellationToken);
    }

    public Task<DiscordTokenResponse> RefreshAsync(
        DiscordOAuthCredentials credentials,
        string refreshToken,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(credentials);
        ArgumentException.ThrowIfNullOrWhiteSpace(refreshToken);

        return PostAsync(new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = refreshToken,
            ["client_id"] = credentials.ClientId,
            ["client_secret"] = credentials.ClientSecret,
        }, cancellationToken);
    }

    private async Task<DiscordTokenResponse> PostAsync(
        IDictionary<string, string> form,
        CancellationToken cancellationToken)
    {
        using var content = new FormUrlEncodedContent(form);
        using var request = new HttpRequestMessage(HttpMethod.Post, TokenEndpoint) { Content = content };
        using var response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);

        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            throw BuildError(response.StatusCode, body);
        }

        var token = JsonSerializer.Deserialize<DiscordTokenResponse>(body, JsonOptions)
            ?? throw new DiscordOAuthException("Token response could not be deserialized.");

        var grantType = form.TryGetValue("grant_type", out var g) ? g : "?";
        logger.LogInformation(
            "OAuth grant {GrantType} succeeded (expires_in={ExpiresIn}s, scope={Scope})",
            grantType, token.ExpiresIn, token.Scope);
        return token;
    }

    private static DiscordOAuthException BuildError(System.Net.HttpStatusCode statusCode, string body)
    {
        string? error = null;
        string? description = null;
        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;
            if (root.TryGetProperty("error", out var e) && e.ValueKind == JsonValueKind.String)
            {
                error = e.GetString();
            }
            if (root.TryGetProperty("error_description", out var d) && d.ValueKind == JsonValueKind.String)
            {
                description = d.GetString();
            }
        }
        catch (JsonException)
        {
            // body wasn't JSON; fall through to generic error
        }

        var summary = description ?? body;
        if (summary.Length > 256)
        {
            summary = summary[..256] + "…";
        }

        return new DiscordOAuthException(
            $"Token endpoint returned {(int)statusCode} {error ?? "error"}: {summary}")
        {
            ErrorCode = error,
            StatusCode = statusCode,
        };
    }
}
