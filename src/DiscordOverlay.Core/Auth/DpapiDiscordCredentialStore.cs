using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DiscordOverlay.Core.Auth;

[SupportedOSPlatform("windows")]
public sealed class DpapiDiscordCredentialStore(
    IOptions<DiscordCredentialStoreOptions> options,
    ILogger<DpapiDiscordCredentialStore> logger) : IDiscordCredentialStore
{
    private static readonly byte[] Entropy = "DiscordOverlay\0v1"u8.ToArray();
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly string filePath = options.Value.FilePath;
    private readonly SemaphoreSlim ioLock = new(1, 1);

    public async Task<DiscordCredentialBundle?> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath))
        {
            return null;
        }

        await ioLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var encrypted = await File.ReadAllBytesAsync(filePath, cancellationToken).ConfigureAwait(false);
            var plaintext = ProtectedData.Unprotect(encrypted, Entropy, DataProtectionScope.CurrentUser);
            return JsonSerializer.Deserialize<DiscordCredentialBundle>(plaintext, JsonOptions);
        }
        catch (CryptographicException ex)
        {
            logger.LogError(ex,
                "Failed to decrypt credential store at {Path}; treating as missing. " +
                "This usually means the file was created under a different Windows user account.",
                filePath);
            return null;
        }
        catch (JsonException ex)
        {
            logger.LogError(ex, "Credential store at {Path} contained invalid JSON; treating as missing.", filePath);
            return null;
        }
        finally
        {
            ioLock.Release();
        }
    }

    public async Task SaveAsync(DiscordCredentialBundle bundle, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(bundle);

        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var plaintext = JsonSerializer.SerializeToUtf8Bytes(bundle, JsonOptions);
        var encrypted = ProtectedData.Protect(plaintext, Entropy, DataProtectionScope.CurrentUser);

        await ioLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var tempFile = filePath + ".tmp";
            await File.WriteAllBytesAsync(tempFile, encrypted, cancellationToken).ConfigureAwait(false);
            File.Move(tempFile, filePath, overwrite: true);
            logger.LogDebug("Credentials saved to {Path}", filePath);
        }
        finally
        {
            ioLock.Release();
        }
    }

    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        await ioLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
                logger.LogInformation("Credential store cleared at {Path}", filePath);
            }
        }
        finally
        {
            ioLock.Release();
        }
    }
}
