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

    /// <summary>
    /// Background opacity as a fraction: <c>0</c> fully transparent, <c>1</c>
    /// fully opaque, decimals allowed (<c>0.75</c>). This is the same scale
    /// StreamKit's own <c>bg_opacity</c> uses, so what you write here is what
    /// the overlay gets.
    /// </summary>
    /// <remarks>
    /// A value above 1 is read as a legacy 0-100 percentage (<c>50</c> becomes
    /// <c>0.5</c>), which is how this setting used to behave, so settings.json
    /// files written before the change keep the background they had.
    /// </remarks>
    public double BackgroundOpacity { get; set; } = 0;

    public bool LimitSpeaking { get; set; } = false;

    public bool SmallAvatars { get; set; } = false;

    public bool HideNames { get; set; } = false;

    /// <summary>
    /// Pin your own profile to the top of the voice stack instead of sorting it
    /// alphabetically with everyone else. StreamKit calls this "Show My Avatar
    /// First".
    /// </summary>
    public bool StreamerAvatarFirst { get; set; } = false;
}
