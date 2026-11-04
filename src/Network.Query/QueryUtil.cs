using System;
using System.Buffers.Binary;
using System.Text;

namespace Network.Query;

public static class QueryUtil
{
    public const ushort QueryMagic = 0xFEFD;

    public static ushort ReadUInt16LittleEndian(ReadOnlySpan<byte> buffer)
        => BinaryPrimitives.ReadUInt16LittleEndian(buffer);

    public static int ReadInt32LittleEndian(ReadOnlySpan<byte> buffer)
        => BinaryPrimitives.ReadInt32LittleEndian(buffer);

    public static void WriteUInt16LittleEndian(Span<byte> destination, ushort value)
        => BinaryPrimitives.WriteUInt16LittleEndian(destination, value);

    public static void WriteInt32LittleEndian(Span<byte> destination, int value)
        => BinaryPrimitives.WriteInt32LittleEndian(destination, value);

    public static QueryPacket CreateChallengeRequest(int sessionId)
        => new(QueryPacketType.Challenge, sessionId, Encoding.UTF8.GetBytes("challenge"));

    public static QueryPacket CreateHandshakeRequest(int sessionId, string serverName)
        => new(QueryPacketType.Handshake, sessionId, Encoding.UTF8.GetBytes(serverName ?? string.Empty));

    public static QueryPacket CreateStatisticsRequest(int sessionId, string statName)
        => new(QueryPacketType.Statistics, sessionId, Encoding.UTF8.GetBytes(statName ?? string.Empty));
}