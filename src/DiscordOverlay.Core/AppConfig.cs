using System.Text.Json;
using DiscordOverlay.Core.Discord;
using DiscordOverlay.Core.Streaming;

namespace DiscordOverlay.Core;

public sealed class AppConfig
{
    public ObsConnectionOptions Obs { get; set; } = new();

    public DiscordVoiceChannelWatcherOptions Watcher { get; set; } = new();

    public StreamKitOverlayOptions Streamkit { get; set; } = new();
}

public static class AppConfigStore
{
    public static string DefaultFilePath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "DiscordOverlay",
        "settings.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
    };

    public static async Task<AppConfig> LoadAsync(string? path = null, CancellationToken cancellationToken = default)
    {
        path ??= DefaultFilePath;
        if (!File.Exists(path))
        {
            return new AppConfig();
        }

        try
        {
            await using var stream = File.OpenRead(path);
            var config = await JsonSerializer
                .DeserializeAsync<AppConfig>(stream, JsonOptions, cancellationToken)
                .ConfigureAwait(false);
            return config ?? new AppConfig();
        }
        catch (JsonException)
        {
            return new AppConfig();
        }
    }

    public static async Task SaveAsync(AppConfig config, string? path = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(config);
        path ??= DefaultFilePath;

        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var temp = path + ".tmp";
        var json = JsonSerializer.Serialize(config, JsonOptions);
        await File.WriteAllTextAsync(temp, json, cancellationToken).ConfigureAwait(false);
        File.Move(temp, path, overwrite: true);
    }
}
