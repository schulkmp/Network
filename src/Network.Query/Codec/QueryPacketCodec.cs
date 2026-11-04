using System;
using System.Buffers.Binary;
using System.Text;
using Network.Query;

namespace Network.Query.Codec;

public static class QueryPacketCodec
{
    public const int HeaderSize = 8;

    public static byte[] Encode(QueryPacket packet)
    {
        ArgumentNullException.ThrowIfNull(packet);

        byte[] payload = packet.Payload ?? Array.Empty<byte>();
        byte[] buffer = new byte[HeaderSize + payload.Length];

        BinaryPrimitives.WriteUInt16LittleEndian(buffer.AsSpan(0, 2), QueryPacket.Magic);
        BinaryPrimitives.WriteUInt16LittleEndian(buffer.AsSpan(2, 2), (ushort)packet.Type);
        BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(4, 4), packet.SessionId);
        payload.CopyTo(buffer.AsSpan(HeaderSize, payload.Length));

        return buffer;
    }

    public static bool TryDecode(ReadOnlySpan<byte> buffer, out QueryPacket? packet)
    {
        packet = null;

        if (buffer.Length < HeaderSize)
        {
            return false;
        }

        ushort magic = BinaryPrimitives.ReadUInt16LittleEndian(buffer.Slice(0, 2));
        if (magic != QueryPacket.Magic)
        {
            return false;
        }

        ushort type = BinaryPrimitives.ReadUInt16LittleEndian(buffer.Slice(2, 2));
        int sessionId = BinaryPrimitives.ReadInt32LittleEndian(buffer.Slice(4, 4));

        byte[] payload = buffer.Slice(HeaderSize).ToArray();
        packet = new QueryPacket((QueryPacketType)type, sessionId, payload);
        return true;
    }

    public static string DecodeString(ReadOnlySpan<byte> payload)
        => Encoding.UTF8.GetString(payload);
}