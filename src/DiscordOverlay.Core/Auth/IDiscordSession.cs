namespace DiscordOverlay.Core.Auth;

public interface IDiscordSession
{
    DiscordCredentialBundle? Current { get; }

    Task<DiscordCredentialBundle> SetupAsync(
        string clientId,
        string clientSecret,
        CancellationToken cancellationToken = default);

    Task<bool> ResumeFromStoreAsync(CancellationToken cancellationToken = default);

    Task SignOutAsync(CancellationToken cancellationToken = default);
}
