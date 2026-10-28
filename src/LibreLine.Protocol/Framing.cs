// SPDX-License-Identifier: GPL-2.0-only
using System.Buffers.Binary;
using System.IO.Compression;
using Google.Protobuf;

namespace LibreLine.Protocol;

/// <summary>Reads length-prefixed packets: a 4-byte big-endian length, then that many bytes of <c>Packet</c>.</summary>
public sealed class PacketReader(Stream stream)
{
    /// <summary>The largest packet accepted. Game states are a few kilobytes; this leaves plenty of room.</summary>
    public const int MaxPacketSize = 4 * 1024 * 1024;

    private readonly byte[] header = new byte[4];

    /// <summary>The next packet, or null when the connection has closed.</summary>
    public async Task<Envelope?> ReadAsync(CancellationToken cancellationToken)
    {
        if (!await FillAsync(header, cancellationToken))
        {
            return null;
        }

        var length = BinaryPrimitives.ReadUInt32BigEndian(header);
        if (length > MaxPacketSize)
        {
            throw new InvalidDataException($"packet of {length} bytes is too large");
        }

        var body = new byte[length];
        if (!await FillAsync(body, cancellationToken))
        {
            return null;
        }

        return PacketCodec.Decode(body);
    }

    private async Task<bool> FillAsync(byte[] buffer, CancellationToken cancellationToken)
    {
        var read = 0;
        while (read < buffer.Length)
        {
            var n = await stream.ReadAsync(buffer.AsMemory(read), cancellationToken);
            if (n == 0)
            {
                return false;
            }

            read += n;
        }

        return true;
    }
}

/// <summary>Writes length-prefixed packets.</summary>
public sealed class PacketWriter(Stream stream)
{
    public async Task WriteAsync(byte[] packet, CancellationToken cancellationToken)
    {
        var frame = new byte[4 + packet.Length];
        BinaryPrimitives.WriteUInt32BigEndian(frame, (uint)packet.Length);
        packet.CopyTo(frame, 4);
        await stream.WriteAsync(frame, cancellationToken);
        await stream.FlushAsync(cancellationToken);
    }

    public Task WriteAsync(long packetId, IMessage message, CancellationToken cancellationToken) =>
        WriteAsync(PacketCodec.Encode(packetId, message), cancellationToken);
}

/// <summary>The zlib-compressed lists (<c>LobbyPlayerList.player_list</c>, <c>LobbyGameList.game_list</c>, ...).</summary>
public static class Zlib
{
    public static ByteString Compress(IMessage message)
    {
        using var output = new MemoryStream();
        using (var zlib = new ZLibStream(output, CompressionLevel.Optimal, leaveOpen: true))
        {
            message.WriteTo(zlib);
        }

        return ByteString.CopyFrom(output.ToArray());
    }

    public static T Decompress<T>(ByteString data) where T : IMessage<T>, new()
    {
        using var input = new ZLibStream(new MemoryStream(data.ToByteArray()), CompressionMode.Decompress);
        var message = new T();
        message.MergeFrom(input);
        return message;
    }
}
