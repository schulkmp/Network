using System;
using System.Text;

namespace Network.Query;

public enum QueryPacketType : ushort
{
    Handshake = 0x00,
    Statistics = 0x01,
    Challenge = 0x09,
    Unknown = 0xFFFF
}

public class QueryPacket
{
    public const ushort Magic = 0xFEFD;

    public QueryPacket(QueryPacketType type, int sessionId, byte[] payload)
    {
        Type = type;
        SessionId = sessionId;
        Payload = payload ?? throw new ArgumentNullException(nameof(payload));
        Timestamp = DateTimeOffset.UtcNow;
    }

    public QueryPacketType Type { get; }
    public int SessionId { get; }
    public byte[] Payload { get; }
    public DateTimeOffset Timestamp { get; }

    public string GetPayloadUtf8() => Encoding.UTF8.GetString(Payload);

    public override string ToString() => $"QueryPacket(Type={Type}, SessionId={SessionId}, PayloadLength={Payload.Length})";
}