using System.Runtime.Versioning;
using DiscordOverlay.Core.Auth;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace DiscordOverlay.Core.Tests.Auth;

[SupportedOSPlatform("windows")]
public class DpapiDiscordCredentialStoreTests : IDisposable
{
    private readonly string tempPath = Path.Combine(
        Path.GetTempPath(),
        $"DiscordOverlay-Tests-{Guid.NewGuid():N}",
        "credentials.bin");

    private readonly DpapiDiscordCredentialStore sut;

    public DpapiDiscordCredentialStoreTests()
    {
        var options = Options.Create(new DiscordCredentialStoreOptions { FilePath = tempPath });
        sut = new DpapiDiscordCredentialStore(options, NullLogger<DpapiDiscordCredentialStore>.Instance);
    }

    public void Dispose()
    {
        var directory = Path.GetDirectoryName(tempPath);
        if (directory is not null && Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task LoadAsync_WhenFileMissing_ReturnsNull()
    {
        var bundle = await sut.LoadAsync();
        Assert.Null(bundle);
    }

    [Fact]
    public async Task SaveAsync_ThenLoadAsync_RoundTripsAllFields()
    {
        var expiresAt = DateTimeOffset.UtcNow.AddDays(7);
        var original = new DiscordCredentialBundle(
            ClientId: "CID",
            ClientSecret: "CSEC",
            RedirectUri: "http://localhost:3000/callback",
            AccessToken: "AT",
            RefreshToken: "RT",
            ExpiresAt: expiresAt);

        await sut.SaveAsync(original);
        var loaded = await sut.LoadAsync();

        Assert.NotNull(loaded);
        Assert.Equal(original.ClientId, loaded!.ClientId);
        Assert.Equal(original.ClientSecret, loaded.ClientSecret);
        Assert.Equal(original.RedirectUri, loaded.RedirectUri);
        Assert.Equal(original.AccessToken, loaded.AccessToken);
        Assert.Equal(original.RefreshToken, loaded.RefreshToken);
        Assert.Equal(original.ExpiresAt, loaded.ExpiresAt);
    }

    [Fact]
    public async Task SaveAsync_PersistsFileEncrypted_NotPlaintext()
    {
        var bundle = new DiscordCredentialBundle("CID", "SUPER_SECRET_VALUE", "http://localhost:3000/callback", null, null, null);

        await sut.SaveAsync(bundle);

        var raw = await File.ReadAllBytesAsync(tempPath);
        Assert.NotEmpty(raw);
        var asText = System.Text.Encoding.UTF8.GetString(raw);
        Assert.DoesNotContain("SUPER_SECRET_VALUE", asText);
    }

    [Fact]
    public async Task ClearAsync_RemovesTheFile()
    {
        var bundle = new DiscordCredentialBundle("CID", "CSEC", "http://localhost:3000/callback", null, null, null);
        await sut.SaveAsync(bundle);
        Assert.True(File.Exists(tempPath));

        await sut.ClearAsync();

        Assert.False(File.Exists(tempPath));
        Assert.Null(await sut.LoadAsync());
    }

    [Fact]
    public async Task LoadAsync_WhenCiphertextCorrupt_ReturnsNullInsteadOfThrowing()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(tempPath)!);
        await File.WriteAllBytesAsync(tempPath, new byte[] { 0x00, 0x01, 0x02, 0x03 });

        var bundle = await sut.LoadAsync();

        Assert.Null(bundle);
    }

    [Fact]
    public void IsTokenExpired_TreatsMissingExpiry_AsExpired()
    {
        var bundle = new DiscordCredentialBundle("CID", "CSEC", "http://localhost:3000/callback", "AT", "RT", null);
        Assert.True(bundle.IsTokenExpired(TimeSpan.FromMinutes(1), DateTimeOffset.UtcNow));
    }

    [Fact]
    public void IsTokenExpired_FactorsInClockSkew()
    {
        var now = DateTimeOffset.UtcNow;
        var almost = new DiscordCredentialBundle("CID", "CSEC", "http://localhost:3000/callback", "AT", "RT", now.AddSeconds(30));
        var safe = new DiscordCredentialBundle("CID", "CSEC", "http://localhost:3000/callback", "AT", "RT", now.AddMinutes(10));

        Assert.True(almost.IsTokenExpired(TimeSpan.FromMinutes(1), now));
        Assert.False(safe.IsTokenExpired(TimeSpan.FromMinutes(1), now));
    }
}
