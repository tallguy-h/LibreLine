// SPDX-License-Identifier: GPL-2.0-only
using Google.Protobuf;

namespace LibreLine.Protocol;

/// <summary>
/// One received packet: its <c>Packet.id</c>, its message type number and the message's bytes. The body is parsed
/// only once the type is known.
/// </summary>
public readonly record struct Envelope(long PacketId, int Type, ByteString Body)
{
    public T Parse<T>() where T : IMessage<T>, new()
    {
        var message = new T();
        message.MergeFrom(Body);
        return message;
    }
}

/// <summary>
/// Reads and writes <c>com.daysofwonder.async.Packet</c> by hand: field 1 is the packet id, field 3 the
/// <c>Message</c>, whose one extension field (number = type) holds the message. This avoids extension registries
/// and keeps unknown message types readable.
/// </summary>
public static class PacketCodec
{
    private const int PacketIdField = 1;
    private const int PacketPayloadField = 3;
    private const int RequestNumberField = 1;

    public static Envelope Decode(ReadOnlySpan<byte> packet)
    {
        long id = 0;
        ByteString? payload = null;
        var input = new CodedInputStream(packet.ToArray());
        uint tag;
        while ((tag = input.ReadTag()) != 0)
        {
            switch (WireFormat.GetTagFieldNumber(tag))
            {
                case PacketIdField when WireFormat.GetTagWireType(tag) == WireFormat.WireType.Varint:
                    id = input.ReadInt64();
                    break;
                case PacketPayloadField when WireFormat.GetTagWireType(tag) == WireFormat.WireType.LengthDelimited:
                    payload = input.ReadBytes();
                    break;
                default:
                    input.SkipLastField();
                    break;
            }
        }

        if (payload is null)
        {
            throw new InvalidDataException("packet without a message");
        }

        var type = 0;
        var body = ByteString.Empty;
        var message = payload.CreateCodedInput();
        while ((tag = message.ReadTag()) != 0)
        {
            var field = WireFormat.GetTagFieldNumber(tag);
            if (field >= 100 && WireFormat.GetTagWireType(tag) == WireFormat.WireType.LengthDelimited)
            {
                type = field;
                body = message.ReadBytes();
            }
            else
            {
                message.SkipLastField();
            }
        }

        if (type == 0)
        {
            throw new InvalidDataException("message without a type");
        }

        return new Envelope(id, type, body);
    }

    public static byte[] Encode(long packetId, IMessage message) =>
        Encode(packetId, MessageTypes.NumberOf(message.GetType()), message.ToByteString());

    public static byte[] Encode(long packetId, int type, ByteString body)
    {
        using var payloadBuffer = new MemoryStream();
        var payload = new CodedOutputStream(payloadBuffer);
        payload.WriteTag(RequestNumberField, WireFormat.WireType.Varint);
        payload.WriteInt32(type);
        payload.WriteTag(type, WireFormat.WireType.LengthDelimited);
        payload.WriteBytes(body);
        payload.Flush();

        using var packetBuffer = new MemoryStream();
        var packet = new CodedOutputStream(packetBuffer);
        packet.WriteTag(PacketIdField, WireFormat.WireType.Varint);
        packet.WriteInt64(packetId);
        packet.WriteTag(PacketPayloadField, WireFormat.WireType.LengthDelimited);
        packet.WriteBytes(ByteString.CopyFrom(payloadBuffer.ToArray()));
        packet.Flush();
        return packetBuffer.ToArray();
    }
}
