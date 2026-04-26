using System.Buffers.Binary;
using System.Text;
using DiscordOverlay.Core.Discord;

namespace DiscordOverlay.Core.Tests.Discord;

public class DiscordIpcFrameTests
{
    [Fact]
    public async Task WriteAsync_Emits_LittleEndian_Header_And_Body()
    {
        using var stream = new MemoryStream();
        var body = "{\"cmd\":\"AUTHORIZE\"}"u8.ToArray();

        await DiscordIpcFrame.WriteAsync(stream, DiscordIpcOpCode.Frame, body, CancellationToken.None);

        var written = stream.ToArray();
        Assert.Equal(8 + body.Length, written.Length);
        Assert.Equal((int)DiscordIpcOpCode.Frame, BinaryPrimitives.ReadInt32LittleEndian(written.AsSpan(0, 4)));
        Assert.Equal(body.Length, BinaryPrimitives.ReadInt32LittleEndian(written.AsSpan(4, 4)));
        Assert.Equal(body, written[8..]);
    }

    [Fact]
    public async Task WriteAsync_Then_ReadAsync_Roundtrips_OpCode_And_Body()
    {
        using var stream = new MemoryStream();
        var body = Encoding.UTF8.GetBytes("hello");

        await DiscordIpcFrame.WriteAsync(stream, DiscordIpcOpCode.Handshake, body, CancellationToken.None);
        stream.Position = 0;

        var (op, readBody) = await DiscordIpcFrame.ReadAsync(stream, CancellationToken.None);

        Assert.Equal(DiscordIpcOpCode.Handshake, op);
        Assert.Equal(body, readBody);
    }

    [Fact]
    public async Task WriteAsync_Empty_Body_Is_Allowed()
    {
        using var stream = new MemoryStream();

        await DiscordIpcFrame.WriteAsync(stream, DiscordIpcOpCode.Pong, ReadOnlyMemory<byte>.Empty, CancellationToken.None);
        stream.Position = 0;

        var (op, body) = await DiscordIpcFrame.ReadAsync(stream, CancellationToken.None);

        Assert.Equal(DiscordIpcOpCode.Pong, op);
        Assert.Empty(body);
    }

    [Fact]
    public async Task ReadAsync_Throws_On_Length_Above_Limit()
    {
        using var stream = new MemoryStream();
        Span<byte> header = stackalloc byte[8];
        BinaryPrimitives.WriteInt32LittleEndian(header[..4], (int)DiscordIpcOpCode.Frame);
        BinaryPrimitives.WriteInt32LittleEndian(header[4..], DiscordIpcFrame.MaxBodyLength + 1);
        stream.Write(header);
        stream.Position = 0;

        await Assert.ThrowsAsync<DiscordRpcException>(async () =>
            await DiscordIpcFrame.ReadAsync(stream, CancellationToken.None));
    }
}
