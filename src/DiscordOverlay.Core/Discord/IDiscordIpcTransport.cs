namespace DiscordOverlay.Core.Discord;

public interface IDiscordIpcTransport
{
    Task<Stream> ConnectAsync(CancellationToken cancellationToken);
}
