using System;
using System.Threading.Tasks;
using Network.Raknet;
using Network.Raknet.Channel.Raknet.Packet;
using Network.Raknet.Handler.Codec.Raknet.Common;

namespace Network.Raknet.Test;

public class UnitTest1
{
    [Fact]
    public void RakDatagramCodec_Decode_EncapsulatedPayload()
    {
        var payload = new byte[] { 1, 2, 3, 4, 5 };
        Assert.True(RakDatagramCodec.TryDecode(payload, null, out var enc));
        Assert.NotNull(enc);
        Assert.Equal(payload, enc!.Payload);
    }

    [Fact]
    public async Task RaknetServer_StartStop_NoExceptions()
    {
        var pipeline = new Channel.Raknet.RakChannelPipeline();
        using var server = new RaknetServer("127.0.0.1", 0, pipeline);

        server.Start();
        await Task.Delay(100);
        server.Stop();

        Assert.NotNull(server);
    }

    [Fact]
    public void RakNetCodec_SplitReassembly_Works()
    {
        // create a payload and split into two fragments using the codec header format
        var payload = new byte[100];
        for (int i = 0; i < payload.Length; i++) payload[i] = (byte)(i & 0xFF);

        int splitId = 12345;
        ushort total = 2;

        // first fragment = payload[0..49]
        var frag1 = new byte[1 + 4 + 2 + 2 + 50];
        frag1[0] = 0x10; // split flag
        BitConverter.GetBytes(splitId).CopyTo(frag1, 1);
        BitConverter.GetBytes(total).CopyTo(frag1, 5);
        BitConverter.GetBytes((ushort)0).CopyTo(frag1, 7);
        Array.Copy(payload, 0, frag1, 9, 50);

        // second fragment = payload[50..99]
        var frag2 = new byte[1 + 4 + 2 + 2 + 50];
        frag2[0] = 0x10;
        BitConverter.GetBytes(splitId).CopyTo(frag2, 1);
        BitConverter.GetBytes(total).CopyTo(frag2, 5);
        BitConverter.GetBytes((ushort)1).CopyTo(frag2, 7);
        Array.Copy(payload, 50, frag2, 9, 50);

        Assert.False(RakNetCodec.TryDecode(frag1, null, out var p1, out _));
        Assert.False(p1 is not null);

        Assert.True(RakNetCodec.TryDecode(frag2, null, out var p2, out _));
        Assert.NotNull(p2);
        Assert.Equal(payload.Length, p2!.Payload.Length);
        Assert.Equal(payload, p2.Payload);
    }

    [Fact]
    public void RakNetCodec_ParseReliabilityAndOrdering_ParsesFields()
    {
        byte reliability = 3; // arbitrary non-zero reliability
        byte flags = (byte)(reliability << 5);

        var messageIndex = new byte[] { 0x00, 0x01, 0x02 };
        var orderingIndex = new byte[] { 0x00, 0x03, 0x04 };
        byte orderingChannel = 7;
        var payload = new byte[] { 9, 8, 7 };

        var buffer = new byte[1 + 3 + 4 + payload.Length];
        buffer[0] = flags;
        Array.Copy(messageIndex, 0, buffer, 1, 3);
        Array.Copy(orderingIndex, 0, buffer, 4, 3);
        buffer[7] = orderingChannel;
        Array.Copy(payload, 0, buffer, 8, payload.Length);

        Assert.True(RakNetCodec.TryDecode(buffer, null, out var enc, out var frame));
        Assert.NotNull(enc);
        Assert.NotNull(frame);
        Assert.Equal(reliability, frame!.Reliability);
        Assert.Equal((uint)((messageIndex[0] << 16) | (messageIndex[1] << 8) | messageIndex[2]), frame.MessageIndex);
        Assert.Equal((uint)((orderingIndex[0] << 16) | (orderingIndex[1] << 8) | orderingIndex[2]), frame.OrderingIndex);
        Assert.Equal(orderingChannel, frame.OrderingChannel);
        Assert.Equal(payload.Length, enc!.Payload.Length);
    }

    [Fact]
    public void RakSlidingWindow_AcceptsAndAcksSequences()
    {
        var win = new Channel.Raknet.RakSlidingWindow();
        Assert.True(win.Accept(1));
        Assert.True(win.Accept(2));
        Assert.False(win.Accept(2)); // duplicate

        var acks = win.TakePendingAcks();
        Assert.Contains((uint)1, acks);
        Assert.Contains((uint)2, acks);
    }

    [Fact]
    public async Task RakServerChannel_SendReliableAndProcessAck_Works()
    {
        // capture what is sent by writer
        byte[]? lastSent = null;
        var writer = new Func<ReadOnlyMemory<byte>, System.Net.IPEndPoint, ValueTask>( (mem, ep) =>
        {
            lastSent = mem.ToArray();
            return ValueTask.CompletedTask;
        });

        var ep = new System.Net.IPEndPoint(System.Net.IPAddress.Loopback, 12345);
        var channel = new Channel.Raknet.RakServerChannel(ep, writer);

        var seq = await channel.SendReliableAsync(new byte[] { 1, 2, 3 });
        Assert.Equal(1, channel.OutstandingCount);
        Assert.NotNull(lastSent);

        // simulate ACK from peer
        channel.ProcessAcks(new uint[] { seq });
        Assert.Equal(0, channel.OutstandingCount);
    }

    [Fact]
    public async Task RakServerChannel_SendReliable_Fragmentation_Works()
    {
        var sends = new List<byte[]>();
        var writer = new Func<ReadOnlyMemory<byte>, System.Net.IPEndPoint, ValueTask>( (mem, ep) =>
        {
            sends.Add(mem.ToArray());
            return ValueTask.CompletedTask;
        });

        var ep = new System.Net.IPEndPoint(System.Net.IPAddress.Loopback, 12345);
        var channel = new Channel.Raknet.RakServerChannel(ep, writer);

        int max = Channel.Raknet.RakConstants.DefaultMaxFragmentPayload;
        var payload = new byte[max * 2 + 10];
        for (int i = 0; i < payload.Length; i++) payload[i] = (byte)(i & 0xFF);

        var seq = await channel.SendReliableAsync(payload);

        // should have enqueued outstanding and sent multiple fragments
        Assert.Equal(1, channel.OutstandingCount);
        Assert.True(sends.Count >= 2);

        // simulate ACK
        channel.ProcessAcks(new uint[] { seq });
        Assert.Equal(0, channel.OutstandingCount);
    }
}