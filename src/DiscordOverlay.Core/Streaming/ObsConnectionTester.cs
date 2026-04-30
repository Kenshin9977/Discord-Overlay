using System.ComponentModel;
using Microsoft.Extensions.Logging;
using OBSStudioClient;
using OBSStudioClient.Enums;

namespace DiscordOverlay.Core.Streaming;

public sealed class ObsConnectionTester(ILogger<ObsConnectionTester> logger)
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(8);

    public async Task<ObsConnectionTestResult> TestAsync(
        string hostname,
        int port,
        string password,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        var deadline = timeout ?? DefaultTimeout;

        using var client = new ObsClient();
        var tcs = new TaskCompletionSource<ObsConnectionTestResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var seenHandshake = false;

        void OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(ObsClient.ConnectionState)) return;

            switch (client.ConnectionState)
            {
                case ConnectionState.Connecting:
                case ConnectionState.Authenticating:
                    seenHandshake = true;
                    break;
                case ConnectionState.Connected:
                    tcs.TrySetResult(ObsConnectionTestResult.Success());
                    break;
                case ConnectionState.Disconnected when seenHandshake:
                    tcs.TrySetResult(ObsConnectionTestResult.Failure(
                        "Authentication failed — check the password (Tools → WebSocket Server Settings → Show Connect Info)."));
                    break;
            }
        }

        client.PropertyChanged += OnPropertyChanged;

        try
        {
            await client.ConnectAsync(
                autoReconnect: false,
                password: password,
                hostname: hostname,
                port: port,
                eventSubscription: EventSubscriptions.None).ConfigureAwait(false);

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(deadline);
            return await tcs.Task.WaitAsync(cts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return ObsConnectionTestResult.Failure(
                $"OBS WebSocket did not respond within {deadline.TotalSeconds:N0}s — is OBS running with WebSocket enabled on {hostname}:{port}?");
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "OBS connection test threw");
            return ObsConnectionTestResult.Failure($"Could not connect: {ex.Message}");
        }
        finally
        {
            client.PropertyChanged -= OnPropertyChanged;
            try { client.Disconnect(); } catch { /* best effort */ }
        }
    }
}

public sealed record ObsConnectionTestResult(bool IsSuccess, string? ErrorMessage)
{
    public static ObsConnectionTestResult Success() => new(true, null);

    public static ObsConnectionTestResult Failure(string error) => new(false, error);
}
