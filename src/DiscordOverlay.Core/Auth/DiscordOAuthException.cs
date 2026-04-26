using System.Net;

namespace DiscordOverlay.Core.Auth;

public class DiscordOAuthException : Exception
{
    public DiscordOAuthException(string message) : base(message) { }

    public DiscordOAuthException(string message, Exception inner) : base(message, inner) { }

    public string? ErrorCode { get; init; }

    public HttpStatusCode? StatusCode { get; init; }
}
