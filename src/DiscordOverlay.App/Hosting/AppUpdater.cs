using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Velopack;
using Velopack.Sources;

namespace DiscordOverlay.App.Hosting;

public sealed class AppUpdater
{
    private const string DefaultGitHubRepository = "https://github.com/kenshin993355/Discord-Overlay";

    private readonly UpdateManager? updateManager;
    private readonly ILogger<AppUpdater> logger;

    public AppUpdater(IConfiguration configuration, ILogger<AppUpdater> logger)
    {
        this.logger = logger;
        var repository = configuration["Update:GitHubRepository"] ?? DefaultGitHubRepository;

        try
        {
            var source = new GithubSource(repository, accessToken: null, prerelease: false);
            updateManager = new UpdateManager(source);
            logger.LogInformation("Update source configured for {Repository}", repository);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Could not initialize update source for {Repository}; updates disabled.",
                repository);
        }
    }

    public bool IsInstalled => updateManager?.IsInstalled == true;

    public string? CurrentVersion => updateManager?.CurrentVersion?.ToString();

    public async Task<UpdateInfo?> CheckForUpdatesAsync(CancellationToken cancellationToken = default)
    {
        if (updateManager is null || !updateManager.IsInstalled)
        {
            logger.LogInformation(
                "Update check skipped (running from a non-installed build, e.g. dotnet run or unpacked exe).");
            return null;
        }

        try
        {
            var update = await updateManager.CheckForUpdatesAsync().ConfigureAwait(false);
            if (update is null)
            {
                logger.LogInformation("No updates available (current: {Version})", CurrentVersion);
            }
            else
            {
                logger.LogInformation(
                    "Update available: {Target} (current {Current})",
                    update.TargetFullRelease.Version, CurrentVersion);
            }
            return update;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Update check failed");
            return null;
        }
    }

    public async Task<bool> DownloadAndApplyAsync(UpdateInfo update, CancellationToken cancellationToken = default)
    {
        if (updateManager is null || !updateManager.IsInstalled) return false;

        try
        {
            await updateManager.DownloadUpdatesAsync(update).ConfigureAwait(false);
            logger.LogInformation("Update {Version} downloaded; restarting to apply…",
                update.TargetFullRelease.Version);
            updateManager.ApplyUpdatesAndRestart(update);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Update download/apply failed");
            return false;
        }
    }
}
