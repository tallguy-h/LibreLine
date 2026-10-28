// SPDX-License-Identifier: GPL-2.0-only
using Com.Daysofwonder.Async;
using Message = Com.Daysofwonder.Mm.Message;
using PingRequest = Com.Daysofwonder.Mm.PingRequest;
using Google.Protobuf;

namespace LibreLine.Protocol.Tests;

public sealed class MessageTypeTests
{
    [Theory]
    [InlineData(typeof(AsyncAuthRequest), 400)]
    [InlineData(typeof(AsyncConnectedRequest), 406)]
    [InlineData(typeof(ActionRequiredRequest), 500)]
    [InlineData(typeof(ErrorRequest), 505)]
    [InlineData(typeof(GameStatusReportRequest), 512)]
    [InlineData(typeof(ActionCommitedRequest), 523)]
    [InlineData(typeof(ClientChatRequest), 525)]
    [InlineData(typeof(UserDataUpdatedRequest), 567)]
    [InlineData(typeof(LobbyGameListRequest), 609)]
    [InlineData(typeof(GameObservedRequest), 652)]
    [InlineData(typeof(PingRequest), 777)]
    public void NumbersComeFromTheExtensions(Type type, int number)
    {
        Assert.Equal(number, MessageTypes.NumberOf(type));
    }

    [Fact]
    public void DescribesMessagesByName()
    {
        Assert.Equal("AsyncAuth (400)", MessageTypes.Describe(400));
        Assert.Equal("unknown (99999)", MessageTypes.Describe(99999));
    }
}

public sealed class PacketCodecTests
{
    [Fact]
    public void EncodesThePacketIdAndTheExtension()
    {
        var bytes = PacketCodec.Encode(17, new PingRequest { Timestamp = 123456789 });
        var packet = Packet.Parser.ParseFrom(bytes);
        Assert.Equal(17, packet.Id);
        Assert.Equal(777, packet.Payload.RequestNumber);

        var envelope = PacketCodec.Decode(bytes);
        Assert.Equal(17, envelope.PacketId);
        Assert.Equal(777, envelope.Type);
        Assert.Equal(123456789, envelope.Parse<PingRequest>().Timestamp);
    }

    [Fact]
    public void ReadsWhatTheGeneratedCodeWrites()
    {
        var message = new Message { RequestNumber = 400 };
        message.SetExtension(AsyncAuthRequest.Extensions.AsyncAuthRequest, new AsyncAuthRequest { Name = "alice", GameType = "TT" });
        var bytes = new Packet { Id = 3, Payload = message }.ToByteArray();

        var envelope = PacketCodec.Decode(bytes);
        Assert.Equal(3, envelope.PacketId);
        Assert.Equal(400, envelope.Type);
        Assert.Equal("alice", envelope.Parse<AsyncAuthRequest>().Name);
    }

    [Fact]
    public void KeepsUnknownTypesReadable()
    {
        var envelope = PacketCodec.Decode(PacketCodec.Encode(5, 4321, ByteString.CopyFromUtf8("x")));
        Assert.Equal(4321, envelope.Type);
    }

    [Fact]
    public void RefusesAPacketWithoutAMessage()
    {
        Assert.Throws<InvalidDataException>(() => PacketCodec.Decode(new Packet { Id = 1 }.ToByteArray()));
    }
}

public sealed class FramingTests
{
    [Fact]
    public async Task FramesAreALengthThenAPacket()
    {
        using var stream = new MemoryStream();
        await new PacketWriter(stream).WriteAsync(9, new PingRequest { Timestamp = 1 }, CancellationToken.None);
        var bytes = stream.ToArray();
        Assert.Equal(bytes.Length - 4, (bytes[0] << 24) | (bytes[1] << 16) | (bytes[2] << 8) | bytes[3]);

        stream.Position = 0;
        var reader = new PacketReader(stream);
        var envelope = await reader.ReadAsync(CancellationToken.None);
        Assert.Equal(9, envelope!.Value.PacketId);
        Assert.Null(await reader.ReadAsync(CancellationToken.None));
    }

    [Fact]
    public async Task RefusesOversizedPackets()
    {
        using var stream = new MemoryStream([0x7f, 0, 0, 0]);
        await Assert.ThrowsAsync<InvalidDataException>(() => new PacketReader(stream).ReadAsync(CancellationToken.None));
    }

    [Fact]
    public void CompressedListsAreZlib()
    {
        var list = new GameList();
        list.Games.Add(new GameDetails { GameId = 99 });
        var compressed = Zlib.Compress(list);
        Assert.Equal(0x78, compressed[0]);
        Assert.Equal(99, Zlib.Decompress<GameList>(compressed).Games[0].GameId);
    }
}
