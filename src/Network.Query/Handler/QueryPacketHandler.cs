using System;
using Network.Query;

namespace Network.Query.Handler;

public sealed class QueryPacketHandler
{
    private readonly Func<QueryPacket, bool>? _challengeHandler;
    private readonly Func<QueryPacket, bool>? _handshakeHandler;
    private readonly Func<QueryPacket, bool>? _statisticsHandler;

    public QueryPacketHandler(
        Func<QueryPacket, bool>? challengeHandler = null,
        Func<QueryPacket, bool>? handshakeHandler = null,
        Func<QueryPacket, bool>? statisticsHandler = null)
    {
        _challengeHandler = challengeHandler ?? (_ => true);
        _handshakeHandler = handshakeHandler ?? (_ => true);
        _statisticsHandler = statisticsHandler ?? (_ => true);
    }

    public bool Handle(QueryPacket packet)
    {
        ArgumentNullException.ThrowIfNull(packet);

        return packet.Type switch
        {
            QueryPacketType.Challenge => _challengeHandler(packet),
            QueryPacketType.Handshake => _handshakeHandler(packet),
            QueryPacketType.Statistics => _statisticsHandler(packet),
            _ => false,
        };
    }
}