using DiscordOverlay.App.Hosting;
using DiscordOverlay.Core.Auth;
using DiscordOverlay.Core.Discord;
using DiscordOverlay.Core.Streaming;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;

namespace DiscordOverlay.App;

internal sealed class Program
{
    private Program() { }

    [STAThread]
    private static int Main(string[] args)
    {
        ApplicationConfiguration.Initialize();

        // Install the Windows Forms synchronization context on the main UI
        // thread before the host builds any singletons, so IUiDispatcher can
        // capture this context and let background services marshal to UI.
        SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());
        var uiSyncContext = SynchronizationContext.Current!;

        var logsDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DiscordOverlay",
            "logs");
        Directory.CreateDirectory(logsDirectory);

        var builder = Host.CreateApplicationBuilder(args);

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
        builder.Services.AddHostedService<AppHostedService>();
        builder.Services.AddSingleton<TrayApplicationContext>();

        using var host = builder.Build();
        var logger = host.Services.GetRequiredService<ILogger<Program>>();

        try
        {
            host.Start();
            logger.LogInformation("Discord-Overlay started");

            var context = host.Services.GetRequiredService<TrayApplicationContext>();
            Application.Run(context);

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
