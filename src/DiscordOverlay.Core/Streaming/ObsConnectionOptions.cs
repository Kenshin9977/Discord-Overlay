namespace DiscordOverlay.Core.Streaming;

public sealed class ObsConnectionOptions
{
    public string Hostname { get; set; } = "localhost";

    public int Port { get; set; } = 4455;

    public string Password { get; set; } = string.Empty;

    public string BrowserSourceName { get; set; } = "Discord-Overlay";

    public bool AutoReconnect { get; set; } = true;
}
