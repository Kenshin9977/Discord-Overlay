namespace DiscordOverlay.Core.Auth;

public interface IDiscordCredentialStore
{
    Task<DiscordCredentialBundle?> LoadAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(DiscordCredentialBundle bundle, CancellationToken cancellationToken = default);

    Task ClearAsync(CancellationToken cancellationToken = default);
}
