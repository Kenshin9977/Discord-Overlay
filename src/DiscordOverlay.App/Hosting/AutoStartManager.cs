using Microsoft.Extensions.Logging;
using Microsoft.Win32;

namespace DiscordOverlay.App.Hosting;

public sealed class AutoStartManager(ILogger<AutoStartManager> logger)
{
    private const string RegistryPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string RegistryValueName = "Discord-Overlay";

    public bool IsEnabled
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegistryPath, writable: false);
            return key?.GetValue(RegistryValueName) is string value && !string.IsNullOrWhiteSpace(value);
        }
    }

    public string? CurrentCommand
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegistryPath, writable: false);
            return key?.GetValue(RegistryValueName) as string;
        }
    }

    public void Enable(string? executablePath = null)
    {
        var path = executablePath ?? Environment.ProcessPath
            ?? throw new InvalidOperationException("Could not determine the executable path for auto-start.");
        var command = path.Contains(' ', StringComparison.Ordinal) ? $"\"{path}\"" : path;

        using var key = Registry.CurrentUser.CreateSubKey(RegistryPath, writable: true)
            ?? throw new InvalidOperationException("Could not open HKCU Run registry key.");
        key.SetValue(RegistryValueName, command, RegistryValueKind.String);
        logger.LogInformation("Auto-start enabled: {Command}", command);
    }

    public void Disable()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RegistryPath, writable: true);
        if (key is null)
        {
            return;
        }

        if (key.GetValue(RegistryValueName) is not null)
        {
            key.DeleteValue(RegistryValueName, throwOnMissingValue: false);
            logger.LogInformation("Auto-start disabled");
        }
    }
}
