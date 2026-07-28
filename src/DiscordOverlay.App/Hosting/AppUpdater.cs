using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Velopack;
using Velopack.Sources;

namespace DiscordOverlay.App.Hosting;

public sealed class AppUpdater
{
    private const string DefaultGitHubRepository = "https://github.com/Kenshin9977/Discord-Overlay";

    private readonly UpdateManager? updateManager;
    private readonly ILogger<AppUpdater> logger;

    /// Whether this build is allowed to replace itself.
    ///
    /// False in the Store edition, and that is the point of the edition. The
    /// Store certifies one specific binary and distributes it; an app that
    /// quietly swaps itself afterwards is running code the Store never saw, and
    /// leaves its listing describing a version nobody is actually running.
    /// There, delivering updates is the Store's job, and this stays out of it.
    public static bool SelfUpdatesAllowed =>
#if STORE_EDITION
        false;
#else
        true;
#endif

    public AppUpdater(IConfiguration configuration, ILogger<AppUpdater> logger)
    {
        this.logger = logger;

        // Left null, every method below already degrades to "nothing to do":
        // IsInstalled is false, CheckForUpdatesAsync returns null and
        // DownloadAndApplyAsync returns false. No second code path to keep in
        // step with the first.
        if (!SelfUpdatesAllowed)
        {
            logger.LogInformation("Self-updating is off in this edition; the Store delivers updates.");
            return;
        }

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
