using DiscordOverlay.Core;

namespace DiscordOverlay.Core.Tests.Configuration;

public class AppConfigStoreTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "dovl-cfg-" + Guid.NewGuid().ToString("N"));

    private string Path_ => Path.Combine(directory, "settings.json");

    public AppConfigStoreTests() => Directory.CreateDirectory(directory);

    public void Dispose()
    {
        try { Directory.Delete(directory, recursive: true); } catch (IOException) { }
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task LoadAsync_ReturnsDefaults_WhenFileDoesNotExist()
    {
        var config = await AppConfigStore.LoadAsync(Path_);
        Assert.Equal(0, config.Streamkit.BackgroundOpacity);
    }

    [Fact]
    public async Task LoadAsync_ReadsDecimalOpacity()
    {
        await File.WriteAllTextAsync(Path_, """{ "Streamkit": { "BackgroundOpacity": 0.75 } }""");

        var config = await AppConfigStore.LoadAsync(Path_);

        Assert.Equal(0.75, config.Streamkit.BackgroundOpacity);
    }

    [Fact]
    public async Task LoadAsync_ReadsQuotedNumbers()
    {
        // What you get from copying a value out of the README, or from an editor
        // that quotes everything. It used to take the whole file down with it.
        await File.WriteAllTextAsync(Path_, """{ "Streamkit": { "BackgroundOpacity": "0.75", "TextSize": "18" } }""");

        var config = await AppConfigStore.LoadAsync(Path_);

        Assert.Equal(0.75, config.Streamkit.BackgroundOpacity);
        Assert.Equal(18, config.Streamkit.TextSize);
    }

    [Fact]
    public async Task LoadAsync_ToleratesCommentsAndTrailingCommas()
    {
        await File.WriteAllTextAsync(Path_, """
            {
              // hand-edited
              "Streamkit": {
                "StreamerAvatarFirst": true,
              },
            }
            """);

        var config = await AppConfigStore.LoadAsync(Path_);

        Assert.True(config.Streamkit.StreamerAvatarFirst);
    }

    [Fact]
    public async Task LoadAsync_KeepsACopy_WhenTheFileCannotBeParsed()
    {
        await File.WriteAllTextAsync(Path_, """{ "Obs": { "Port": ] }""");

        var config = await AppConfigStore.LoadAsync(Path_);

        // Defaults so the app still starts...
        Assert.Equal(4455, config.Obs.Port);
        // ...but not at the cost of what the user wrote, since the next save
        // writes those defaults over settings.json.
        var preserved = Path.Combine(directory, "settings.invalid.json");
        Assert.True(File.Exists(preserved), "the unparseable settings file should be preserved");
        Assert.Contains("\"Port\"", await File.ReadAllTextAsync(preserved));
    }

    [Fact]
    public async Task SaveAsync_RoundTrips()
    {
        var config = new AppConfig();
        config.Streamkit.BackgroundOpacity = 0.75;
        config.Streamkit.StreamerAvatarFirst = true;
        config.Obs.BrowserSourceName = "Overlay";

        await AppConfigStore.SaveAsync(config, Path_);
        var reloaded = await AppConfigStore.LoadAsync(Path_);

        Assert.Equal(0.75, reloaded.Streamkit.BackgroundOpacity);
        Assert.True(reloaded.Streamkit.StreamerAvatarFirst);
        Assert.Equal("Overlay", reloaded.Obs.BrowserSourceName);
    }
}
