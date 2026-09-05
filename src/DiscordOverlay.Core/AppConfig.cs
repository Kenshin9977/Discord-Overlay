using System.Text.Json;
using System.Text.Json.Serialization;
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

        // This file is documented as hand-editable, so read it the way a person
        // writes one: a quoted number ("0.75"), a trailing comma, a // note.
        // Rejecting those costs the user every other setting in the file, since
        // a parse failure here falls back to defaults and the next save writes
        // them over the top.
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
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
            // Defaults keep the app running, but the next save would overwrite
            // the file with them. Keep the user's version so a typo costs a
            // rename, not the whole configuration.
            TryPreserveUnreadableFile(path);
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

    /// <summary>
    /// Copy a settings file we could not parse next to itself, so its contents
    /// survive being replaced by defaults. Best-effort by design: failing to
    /// take the backup must not stop the app from starting.
    /// </summary>
    private static void TryPreserveUnreadableFile(string path)
    {
        try
        {
            File.Copy(path, Path.ChangeExtension(path, ".invalid.json"), overwrite: true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
