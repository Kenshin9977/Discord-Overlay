namespace DiscordOverlay.Core.Discord;

public class DiscordRpcException : Exception
{
    public DiscordRpcException(string message) : base(message) { }

    public DiscordRpcException(string message, Exception inner) : base(message, inner) { }

    public string? ErrorCode { get; init; }
}
