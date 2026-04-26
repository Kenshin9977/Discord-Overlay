using System.Buffers.Binary;

namespace DiscordOverlay.Core.Discord;

internal static class DiscordIpcFrame
{
    public const int HeaderSize = 8;
    public const int MaxBodyLength = 64 * 1024;

    public static async Task WriteAsync(
        Stream stream,
        DiscordIpcOpCode op,
        ReadOnlyMemory<byte> body,
        CancellationToken cancellationToken)
    {
        var header = new byte[HeaderSize];
        BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(0, 4), (int)op);
        BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(4, 4), body.Length);
        await stream.WriteAsync(header, cancellationToken).ConfigureAwait(false);
        if (!body.IsEmpty)
        {
            await stream.WriteAsync(body, cancellationToken).ConfigureAwait(false);
        }
        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    public static async Task<(DiscordIpcOpCode Op, byte[] Body)> ReadAsync(
        Stream stream,
        CancellationToken cancellationToken)
    {
        var header = new byte[HeaderSize];
        await stream.ReadExactlyAsync(header, cancellationToken).ConfigureAwait(false);
        var op = BinaryPrimitives.ReadInt32LittleEndian(header.AsSpan(0, 4));
        var length = BinaryPrimitives.ReadInt32LittleEndian(header.AsSpan(4, 4));

        if (length < 0 || length > MaxBodyLength)
        {
            throw new DiscordRpcException($"Invalid IPC frame length: {length}");
        }

        var body = length == 0 ? [] : new byte[length];
        if (length > 0)
        {
            await stream.ReadExactlyAsync(body, cancellationToken).ConfigureAwait(false);
        }

        return ((DiscordIpcOpCode)op, body);
    }
}
