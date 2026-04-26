using System.IO.Pipes;
using Microsoft.Extensions.Logging;

namespace DiscordOverlay.Core.Discord;

public sealed class NamedPipeIpcTransport(ILogger<NamedPipeIpcTransport> logger) : IDiscordIpcTransport
{
    private const int MaxPipeIndex = 9;
    private const int ConnectTimeoutMilliseconds = 250;

    public async Task<Stream> ConnectAsync(CancellationToken cancellationToken)
    {
        for (var index = 0; index <= MaxPipeIndex; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var pipeName = $"discord-ipc-{index}";
            var pipe = new NamedPipeClientStream(
                serverName: ".",
                pipeName: pipeName,
                direction: PipeDirection.InOut,
                options: PipeOptions.Asynchronous);

            try
            {
                await pipe.ConnectAsync(ConnectTimeoutMilliseconds, cancellationToken).ConfigureAwait(false);
                logger.LogInformation("Connected to Discord IPC pipe {PipeName}", pipeName);
                return pipe;
            }
            catch (TimeoutException)
            {
                await pipe.DisposeAsync().ConfigureAwait(false);
            }
            catch (IOException) when (!cancellationToken.IsCancellationRequested)
            {
                await pipe.DisposeAsync().ConfigureAwait(false);
            }
            catch
            {
                await pipe.DisposeAsync().ConfigureAwait(false);
                throw;
            }
        }

        throw new DiscordRpcException(
            "No Discord IPC pipe available (tried discord-ipc-0..9). Is Discord running?");
    }
}
