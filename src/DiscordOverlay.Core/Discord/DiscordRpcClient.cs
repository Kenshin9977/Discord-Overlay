using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace DiscordOverlay.Core.Discord;

public sealed class DiscordRpcClient : IDiscordRpcClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    private static readonly TimeSpan DefaultRequestTimeout = TimeSpan.FromSeconds(15);

    private readonly IDiscordIpcTransport transport;
    private readonly ILogger<DiscordRpcClient> logger;
    private readonly SemaphoreSlim writeLock = new(1, 1);
    private readonly ConcurrentDictionary<string, TaskCompletionSource<DiscordIpcMessage>> pending = new();
    private readonly object stateLock = new();

    private Stream? stream;
    private string? clientId;
    private CancellationTokenSource? readLoopCts;
    private Task? readLoopTask;
    private TaskCompletionSource<DiscordIpcMessage>? handshakeReady;
    private int disposed;

    public DiscordRpcClient(IDiscordIpcTransport transport, ILogger<DiscordRpcClient> logger)
    {
        this.transport = transport;
        this.logger = logger;
    }

    public event EventHandler<DiscordVoiceChannelSelectedEventArgs>? VoiceChannelSelected;

    public event EventHandler? Disconnected;

    public bool IsConnected
    {
        get
        {
            lock (stateLock)
            {
                return stream is not null && Volatile.Read(ref disposed) == 0;
            }
        }
    }

    public async Task ConnectAsync(string clientId, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref disposed) != 0, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(clientId);

        lock (stateLock)
        {
            if (stream is not null)
            {
                throw new InvalidOperationException("Discord RPC client is already connected.");
            }
        }

        var newStream = await transport.ConnectAsync(cancellationToken).ConfigureAwait(false);
        var newCts = new CancellationTokenSource();
        var newReady = new TaskCompletionSource<DiscordIpcMessage>(TaskCreationOptions.RunContinuationsAsynchronously);

        lock (stateLock)
        {
            stream = newStream;
            this.clientId = clientId;
            readLoopCts = newCts;
            handshakeReady = newReady;
        }

        readLoopTask = Task.Run(() => ReadLoopAsync(newStream, newCts.Token), CancellationToken.None);

        var handshake = new DiscordIpcHandshake { Version = 1, ClientId = clientId };
        var body = JsonSerializer.SerializeToUtf8Bytes(handshake, JsonOptions);
        await DiscordIpcFrame.WriteAsync(newStream, DiscordIpcOpCode.Handshake, body, cancellationToken)
            .ConfigureAwait(false);

        await newReady.Task.WaitAsync(DefaultRequestTimeout, cancellationToken).ConfigureAwait(false);
        logger.LogInformation("Discord IPC handshake complete (client_id={ClientId})", clientId);
    }

    public async Task<string> AuthorizeAsync(
        IReadOnlyList<string> scopes,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scopes);
        var id = clientId ?? throw new InvalidOperationException("Connect must be called before Authorize.");

        var args = new DiscordAuthorizeArgs { ClientId = id, Scopes = scopes };
        var response = await SendCommandAsync("AUTHORIZE", args, cancellationToken).ConfigureAwait(false);

        if (response.Data is not { } data || !data.TryGetProperty("code", out var codeElement))
        {
            throw new DiscordRpcException("AUTHORIZE response did not contain a code.");
        }

        return codeElement.GetString() ?? throw new DiscordRpcException("AUTHORIZE code was null.");
    }

    public async Task<DiscordAuthenticateResult> AuthenticateAsync(
        string accessToken,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accessToken);

        var args = new DiscordAuthenticateArgs { AccessToken = accessToken };
        var response = await SendCommandAsync("AUTHENTICATE", args, cancellationToken).ConfigureAwait(false);

        if (response.Data is not { } data)
        {
            throw new DiscordRpcException("AUTHENTICATE response did not contain data.");
        }

        var result = data.Deserialize<DiscordAuthenticateResult>(JsonOptions)
            ?? throw new DiscordRpcException("AUTHENTICATE response could not be deserialized.");

        logger.LogInformation("Authenticated as {Username} (id={UserId})",
            result.User.GlobalName ?? result.User.Username, result.User.Id);
        return result;
    }

    public Task SubscribeVoiceChannelSelectAsync(CancellationToken cancellationToken = default)
        => SubscribeAsync("VOICE_CHANNEL_SELECT", cancellationToken);

    private async Task SubscribeAsync(string evt, CancellationToken cancellationToken)
    {
        var nonce = Guid.NewGuid().ToString("N");
        var request = new DiscordIpcRequest { Cmd = "SUBSCRIBE", Nonce = nonce, Evt = evt };
        await SendInternalAsync(request, cancellationToken).ConfigureAwait(false);
    }

    public async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        Stream? toClose;
        CancellationTokenSource? toCancel;
        Task? toAwait;

        lock (stateLock)
        {
            toClose = stream;
            toCancel = readLoopCts;
            toAwait = readLoopTask;
            stream = null;
            readLoopCts = null;
            readLoopTask = null;
        }

        if (toCancel is not null)
        {
            await toCancel.CancelAsync().ConfigureAwait(false);
            toCancel.Dispose();
        }

        if (toClose is not null)
        {
            try
            {
                await DiscordIpcFrame.WriteAsync(toClose, DiscordIpcOpCode.Close, ReadOnlyMemory<byte>.Empty, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch
            {
                // Best-effort close; pipe may already be torn down.
            }
            await toClose.DisposeAsync().ConfigureAwait(false);
        }

        if (toAwait is not null)
        {
            try
            {
                await toAwait.ConfigureAwait(false);
            }
            catch
            {
                // Read loop swallows expected errors; log noise is unwanted.
            }
        }

        FailPending(new DiscordRpcException("Discord RPC client disconnected."));
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0)
        {
            return;
        }

        await DisconnectAsync(CancellationToken.None).ConfigureAwait(false);
        writeLock.Dispose();
    }

    private async Task<DiscordIpcMessage> SendCommandAsync(
        string cmd,
        object? args,
        CancellationToken cancellationToken)
    {
        var nonce = Guid.NewGuid().ToString("N");
        var request = new DiscordIpcRequest { Cmd = cmd, Nonce = nonce, Args = args };
        return await SendInternalAsync(request, cancellationToken).ConfigureAwait(false);
    }

    private async Task<DiscordIpcMessage> SendInternalAsync(
        DiscordIpcRequest request,
        CancellationToken cancellationToken)
    {
        var s = stream ?? throw new InvalidOperationException("Connect must be called before sending commands.");

        var tcs = new TaskCompletionSource<DiscordIpcMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!pending.TryAdd(request.Nonce, tcs))
        {
            throw new InvalidOperationException("Duplicate nonce — should not happen.");
        }

        try
        {
            var body = JsonSerializer.SerializeToUtf8Bytes(request, JsonOptions);

            await writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                await DiscordIpcFrame.WriteAsync(s, DiscordIpcOpCode.Frame, body, cancellationToken)
                    .ConfigureAwait(false);
            }
            finally
            {
                writeLock.Release();
            }

            return await tcs.Task.WaitAsync(DefaultRequestTimeout, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            pending.TryRemove(request.Nonce, out _);
        }
    }

    private async Task ReadLoopAsync(Stream s, CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var (op, body) = await DiscordIpcFrame.ReadAsync(s, cancellationToken).ConfigureAwait(false);
                HandleFrame(op, body, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Graceful shutdown.
        }
        catch (EndOfStreamException)
        {
            logger.LogInformation("Discord IPC pipe closed by peer.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Discord IPC read loop terminated unexpectedly.");
        }
        finally
        {
            FailPending(new DiscordRpcException("Discord IPC connection closed."));
            try
            {
                Disconnected?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Disconnected handler threw.");
            }
        }
    }

    private void HandleFrame(DiscordIpcOpCode op, byte[] body, CancellationToken cancellationToken)
    {
        switch (op)
        {
            case DiscordIpcOpCode.Frame:
                DispatchFrame(body);
                break;
            case DiscordIpcOpCode.Ping:
                _ = WritePongAsync(body, cancellationToken);
                break;
            case DiscordIpcOpCode.Pong:
                break;
            case DiscordIpcOpCode.Close:
                throw new DiscordRpcException("Discord IPC peer requested close.");
            default:
                logger.LogWarning("Unknown Discord IPC opcode: {OpCode}", (int)op);
                break;
        }
    }

    private async Task WritePongAsync(byte[] body, CancellationToken cancellationToken)
    {
        var s = stream;
        if (s is null) return;
        try
        {
            await writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                await DiscordIpcFrame.WriteAsync(s, DiscordIpcOpCode.Pong, body, cancellationToken)
                    .ConfigureAwait(false);
            }
            finally
            {
                writeLock.Release();
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Failed to send PONG.");
        }
    }

    private void DispatchFrame(byte[] body)
    {
        DiscordIpcMessage? message;
        try
        {
            message = JsonSerializer.Deserialize<DiscordIpcMessage>(body, JsonOptions);
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Failed to deserialize Discord IPC frame.");
            return;
        }

        if (message is null) return;

        if (message.Cmd == "DISPATCH" && message.Evt == "READY")
        {
            handshakeReady?.TrySetResult(message);
            return;
        }

        if (message.Evt == "ERROR" && message.Nonce is { } errorNonce
            && pending.TryRemove(errorNonce, out var errorTcs))
        {
            errorTcs.TrySetException(BuildError(message));
            return;
        }

        if (message.Nonce is { } nonce && pending.TryRemove(nonce, out var tcs))
        {
            tcs.TrySetResult(message);
            return;
        }

        if (message.Cmd == "DISPATCH")
        {
            DispatchSubscriptionEvent(message);
        }
    }

    private void DispatchSubscriptionEvent(DiscordIpcMessage message)
    {
        switch (message.Evt)
        {
            case "VOICE_CHANNEL_SELECT":
                if (message.Data is not { } data) return;
                var channelId = data.TryGetProperty("channel_id", out var cid) && cid.ValueKind == JsonValueKind.String
                    ? cid.GetString() : null;
                var guildId = data.TryGetProperty("guild_id", out var gid) && gid.ValueKind == JsonValueKind.String
                    ? gid.GetString() : null;
                try
                {
                    VoiceChannelSelected?.Invoke(this, new DiscordVoiceChannelSelectedEventArgs
                    {
                        ChannelId = channelId,
                        GuildId = guildId,
                    });
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "VoiceChannelSelected handler threw.");
                }
                break;
            default:
                logger.LogDebug("Unhandled DISPATCH event: {Event}", message.Evt);
                break;
        }
    }

    private static DiscordRpcException BuildError(DiscordIpcMessage message)
    {
        string? code = null;
        string? text = null;
        if (message.Data is { } data)
        {
            if (data.TryGetProperty("code", out var c))
            {
                code = c.ValueKind == JsonValueKind.Number ? c.GetInt32().ToString() : c.GetString();
            }
            if (data.TryGetProperty("message", out var m))
            {
                text = m.GetString();
            }
        }

        return new DiscordRpcException($"Discord RPC error {code ?? "?"}: {text ?? "unknown"}")
        {
            ErrorCode = code,
        };
    }

    private void FailPending(Exception error)
    {
        foreach (var nonce in pending.Keys)
        {
            if (pending.TryRemove(nonce, out var tcs))
            {
                tcs.TrySetException(error);
            }
        }
        handshakeReady?.TrySetException(error);
    }
}
