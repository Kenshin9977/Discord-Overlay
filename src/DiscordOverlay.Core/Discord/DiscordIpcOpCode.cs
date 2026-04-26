namespace DiscordOverlay.Core.Discord;

internal enum DiscordIpcOpCode
{
    Handshake = 0,
    Frame = 1,
    Close = 2,
    Ping = 3,
    Pong = 4,
}
