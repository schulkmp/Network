using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Network.Query;
using Network.Query.Codec;
using Network.Query.Handler;
using Network.Query.Packets;

namespace Network.Query.Test;

public class UnitTest1
{
    [Fact]
    public void QueryPacket_CreatePacket_HasExpectedValues()
    {
        var payload = new byte[] { 1, 2, 3, 4 };

        var packet = new QueryPacket(QueryPacketType.Statistics, 42, payload);

        Assert.Equal(QueryPacketType.Statistics, packet.Type);
        Assert.Equal(42, packet.SessionId);
        Assert.Equal(payload, packet.Payload);
        Assert.True(packet.Timestamp <= DateTimeOffset.UtcNow);
    }

    [Fact]
    public void QueryPacketCodec_EncodeAndDecode_RoundTrips()
    {
        var packet = QueryUtil.CreateStatisticsRequest(77, "status");

        byte[] encoded = QueryPacketCodec.Encode(packet);

        Assert.True(QueryPacketCodec.TryDecode(encoded, out var decoded));
        Assert.NotNull(decoded);
        Assert.Equal(packet.SessionId, decoded!.SessionId);
        Assert.Equal(packet.Type, decoded.Type);
        Assert.Equal(packet.GetPayloadUtf8(), decoded.GetPayloadUtf8());
    }

    [Fact]
    public void QueryPacketCodec_Decode_InvalidMagic_ReturnsFalse()
    {
        var buffer = new byte[]
        {
            0x00, 0x00,
            0x00, 0x00,
            0x00, 0x00, 0x00, 0x00
        };

        Assert.False(QueryPacketCodec.TryDecode(buffer, out _));
    }

    [Fact]
    public void QueryPacketHandler_HandlesChallengePacket()
    {
        var handled = false;
        var handler = new QueryPacketHandler(
            challengeHandler: packet =>
            {
                handled = true;
                Assert.Equal(QueryPacketType.Challenge, packet.Type);
                return true;
            });

        bool result = handler.Handle(QueryUtil.CreateChallengeRequest(1));

        Assert.True(result);
        Assert.True(handled);
    }

    [Fact]
    public void QueryPacketHandler_HandlesHandshakePacket()
    {
        var handled = false;
        var handler = new QueryPacketHandler(
            handshakeHandler: packet =>
            {
                handled = true;
                Assert.Equal(QueryPacketType.Handshake, packet.Type);
                return true;
            });

        bool result = handler.Handle(QueryUtil.CreateHandshakeRequest(2, "Schulk Server"));

        Assert.True(result);
        Assert.True(handled);
    }

    [Fact]
    public void QueryPacketHandler_HandlesStatisticsPacket()
    {
        var handled = false;
        var handler = new QueryPacketHandler(
            statisticsHandler: packet =>
            {
                handled = true;
                Assert.Equal(QueryPacketType.Statistics, packet.Type);
                return true;
            });

        bool result = handler.Handle(QueryUtil.CreateStatisticsRequest(3, "list"));

        Assert.True(result);
        Assert.True(handled);
    }

    [Fact]
    public void QueryUtil_CreatePacketHelpers_ReturnExpectedPacketTypes()
    {
        var challenge = QueryUtil.CreateChallengeRequest(10);
        var handshake = QueryUtil.CreateHandshakeRequest(11, "Server");
        var statistics = QueryUtil.CreateStatisticsRequest(12, "players");

        Assert.Equal(QueryPacketType.Challenge, challenge.Type);
        Assert.Equal(QueryPacketType.Handshake, handshake.Type);
        Assert.Equal(QueryPacketType.Statistics, statistics.Type);
    }

    [Fact]
    public void HandshakePacket_CreatesPayloadFromMetadata()
    {
        var packet = new HandshakePacket(5, "MyServer", "World", 1, 2, 10, 7);

        Assert.Equal(QueryPacketType.Handshake, packet.Type);
        Assert.Equal("MyServer", packet.ServerName);
        Assert.Equal("World", packet.LevelName);
        Assert.Equal(1, packet.ProtocolVersion);
        Assert.Equal(2, packet.Version);
        Assert.Equal(10, packet.MaxPlayers);
        Assert.Equal(7, packet.OnlinePlayers);
        Assert.Contains("server=MyServer", packet.GetPayloadUtf8());
    }

    [Fact]
    public void StatisticsPacket_CreatesPayloadFromValues()
    {
        var values = new Dictionary<string, string>
        {
            ["players"] = "10",
            ["version"] = "1.21.0"
        };

        var packet = new StatisticsPacket(6, values);

        Assert.Equal(QueryPacketType.Statistics, packet.Type);
        Assert.Equal(2, packet.Values.Count);
        Assert.Equal("10", packet.Values["players"]);
        Assert.Equal("1.21.0", packet.Values["version"]);
    }

    [Fact]
    public async Task QueryNetworkListener_StartAndStop_CanExistWithoutThrowing()
    {
        var packets = new List<QueryPacket>();
        using var listener = new QueryNetworkListener("127.0.0.1", 0, packet => packets.Add(packet));

        listener.Start();
        await Task.Delay(100);
        listener.Stop();

        Assert.NotNull(listener);
    }
}