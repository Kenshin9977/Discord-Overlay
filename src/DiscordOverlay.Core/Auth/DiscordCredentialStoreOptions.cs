namespace DiscordOverlay.Core.Auth;

public sealed class DiscordCredentialStoreOptions
{
    public string FilePath { get; set; } = DefaultFilePath;

    public static string DefaultFilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "DiscordOverlay",
        "credentials.bin");
}
