namespace DiscordOverlay.Core.Streaming;

public sealed class StreamKitOverlayOptions
{
    public bool ShowIcon { get; set; } = true;

    public bool OnlineOnly { get; set; } = true;

    public string Logo { get; set; } = "white";

    public string TextColor { get; set; } = "#ffffff";

    public int TextSize { get; set; } = 14;

    public string TextOutlineColor { get; set; } = "#000000";

    public int TextOutlineSize { get; set; } = 0;

    public string BackgroundColor { get; set; } = "#1e2124";

    public int BackgroundOpacity { get; set; } = 0;

    public bool LimitSpeaking { get; set; } = false;

    public bool SmallAvatars { get; set; } = false;

    public bool HideNames { get; set; } = false;
}
