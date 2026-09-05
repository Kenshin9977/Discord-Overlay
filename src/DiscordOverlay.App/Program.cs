using System.IO;
using System.Windows;
using System.Windows.Threading;
using DiscordOverlay.App.Hosting;
using DiscordOverlay.Core;
using DiscordOverlay.Core.Auth;
using DiscordOverlay.Core.Discord;
using DiscordOverlay.Core.Streaming;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;
using Velopack;
using Wpf.Ui.Appearance;

namespace DiscordOverlay.App;

internal sealed class Program
{
    private Program() { }

    [STAThread]
    private static int Main(string[] args)
    {
        // Velopack hook: handles --silent install, post-install/uninstall,
        // first-run, and restart-after-update before any other code runs.
        VelopackApp.Build().Run();

        // Install the WPF dispatcher's synchronization context on this thread
        // before the host builds any singletons, so IUiDispatcher can capture it
        // and let background services marshal to the UI. Touching
        // CurrentDispatcher is what creates the dispatcher for this thread;
        // Application.Run below then pumps it.
        var dispatcher = Dispatcher.CurrentDispatcher;
        SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(dispatcher));
        var uiSyncContext = SynchronizationContext.Current!;

        var logsDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DiscordOverlay",
            "logs");
        Directory.CreateDirectory(logsDirectory);

        var builder = Host.CreateApplicationBuilder(args);

        // Layer the user's settings.json on top of the bundled appsettings.json.
        // This is the file the settings windows write; reloadOnChange propagates
        // edits to anything reading IOptionsMonitor<T>.CurrentValue.
        builder.Configuration.AddJsonFile(
            AppConfigStore.DefaultFilePath,
            optional: true,
            reloadOnChange: true);

        builder.Services.Configure<ObsConnectionOptions>(builder.Configuration.GetSection("Obs"));
        builder.Services.Configure<DiscordVoiceChannelWatcherOptions>(builder.Configuration.GetSection("Watcher"));
        builder.Services.Configure<StreamKitOverlayOptions>(builder.Configuration.GetSection("Streamkit"));

        builder.Services.AddSerilog((services, lc) => lc
            .ReadFrom.Services(services)
            .ReadFrom.Configuration(builder.Configuration)
            .Enrich.FromLogContext()
            .MinimumLevel.Is(LogEventLevel.Information)
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .MinimumLevel.Override("System", LogEventLevel.Warning)
            .WriteTo.Debug()
            .WriteTo.File(
                path: Path.Combine(logsDirectory, "app-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 14,
                shared: true,
                outputTemplate: "[{Timestamp:HH:mm:ss.fff} {Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}"));

        builder.Services.AddDiscordRpcClient();
        builder.Services.AddDiscordVoiceChannelWatcher();
        builder.Services.AddDiscordOAuth();
        builder.Services.AddDpapiCredentialStore();
        builder.Services.AddDiscordSession();
        builder.Services.AddObsBrowserSourceUpdater();
        builder.Services.AddSingleton<IUiDispatcher>(_ => new UiDispatcher(uiSyncContext));
        builder.Services.AddSingleton<AutoStartManager>();
        builder.Services.AddSingleton<AppUpdater>();
        builder.Services.AddHostedService<AppHostedService>();
        builder.Services.AddSingleton<TrayApplication>();

        using var host = builder.Build();
        var logger = host.Services.GetRequiredService<ILogger<Program>>();

        try
        {
            // The resource dictionaries in App.xaml have to be loaded before the
            // tray icon builds its context menu, or the menu resolves none of
            // the Fluent styles and renders as bare WPF.
            var app = new App();
            app.InitializeComponent();
            ApplicationThemeManager.ApplySystemTheme();

            host.Start();
            logger.LogInformation("Discord-Overlay started");

            // Resolved after the host starts and after the app exists: creating
            // it puts the icon in the notification area.
            using var tray = host.Services.GetRequiredService<TrayApplication>();

            app.Run();

            logger.LogInformation("Discord-Overlay shutting down");
            host.StopAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();
            return 0;
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex, "Discord-Overlay crashed");
            return 1;
        }
    }
}
