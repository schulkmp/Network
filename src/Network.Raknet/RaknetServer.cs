using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Network.Raknet.Handler.Codec.Raknet.Common;
using Network.Raknet.Channel.Raknet.Packet;

namespace Network.Raknet;

public sealed class RaknetServer : IDisposable
{
    private readonly UdpClient _udpClient;
    private readonly CancellationTokenSource _cts = new();
    private readonly Channel.Raknet.RakChannelPipeline _pipeline;
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, Channel.Raknet.RakServerChannel> _channels = new();
    private Task? _loopTask;

    public RaknetServer(string host, int port, Channel.Raknet.RakChannelPipeline pipeline)
    {
        _udpClient = new UdpClient(new IPEndPoint(IPAddress.Parse(host), port));
        _pipeline = pipeline ?? throw new ArgumentNullException(nameof(pipeline));
    }

    public void Start()
    {
        if (_loopTask is not null) return;
        _pipeline.FireChannelRegistered();
        _pipeline.FireChannelActive();
        _loopTask = Task.Run(() => ReceiveLoopAsync(_cts.Token));
    }

    public void Stop()
    {
        _cts.Cancel();
        _udpClient.Close();
        _pipeline.FireChannelInactive();
    }

    private async Task ReceiveLoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            UdpReceiveResult result;
            try
            {
                result = await _udpClient.ReceiveAsync(token);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (SocketException ex)
            {
                _pipeline.FireExceptionCaught(ex);
                continue;
            }

            try
            {
                var datagram = new RakDatagramPacket(result.RemoteEndPoint, result.Buffer);

                // detect control ACK packet: [0]=0x00 (control), [1]=0x01 (ACK), [2..3]=ushort count (LE), then count * 3 bytes messageIndex (big-endian 3 bytes)
                if (result.Buffer.Length >= 2 && result.Buffer[0] == 0x00 && result.Buffer[1] == 0x01)
                {
                    if (result.Buffer.Length < 4) continue;
                    ushort count = BitConverter.ToUInt16(result.Buffer, 2);
                    int expected = 4 + count * 3;
                    if (result.Buffer.Length < expected) continue;

                    var seqs = new uint[count];
                    int pos = 4;
                    for (int i = 0; i < count; i++)
                    {
                        seqs[i] = (uint)((result.Buffer[pos] << 16) | (result.Buffer[pos + 1] << 8) | result.Buffer[pos + 2]);
                        pos += 3;
                    }

                    var key = result.RemoteEndPoint.ToString();
                    if (_channels.TryGetValue(key, out var ch))
                    {
                        ch.ProcessAcks(seqs);
                    }

                    continue;
                }

                if (RakNetCodec.TryDecode(result.Buffer, result.RemoteEndPoint, out var encapsulated, out var frame) && encapsulated is not null)
                {
                    // per-remote channel handling
                    var key = result.RemoteEndPoint.ToString();
                    var channel = _channels.GetOrAdd(key, k => new Channel.Raknet.RakServerChannel(result.RemoteEndPoint, WriteToRemoteAsync));

                    // ack tracking
                    if (frame?.MessageIndex is uint mi)
                    {
                        channel.SlidingWindow.Accept(mi);
                        var toAck = channel.SlidingWindow.TakePendingAcks();
                        if (toAck.Length > 0)
                        {
                            // build ACK control packet
                            int count = toAck.Length;
                            var ack = new byte[4 + count * 3];
                            ack[0] = 0x00;
                            ack[1] = 0x01;
                            BitConverter.GetBytes((ushort)count).CopyTo(ack, 2);
                            int pos = 4;
                            foreach (var s in toAck)
                            {
                                ack[pos] = (byte)((s >> 16) & 0xFF);
                                ack[pos + 1] = (byte)((s >> 8) & 0xFF);
                                ack[pos + 2] = (byte)(s & 0xFF);
                                pos += 3;
                            }

                            _ = WriteToRemoteAsync(ack, result.RemoteEndPoint);
                        }
                    }

                    // forward to channel pipeline
                    var msg = new Channel.Raknet.Packet.RakMessage(encapsulated);
                    channel.Pipeline.FireChannelRead(msg);
                }
            }
            catch (Exception ex)
            {
                _pipeline.FireExceptionCaught(ex);
            }
        }
    }

    private async ValueTask WriteToRemoteAsync(ReadOnlyMemory<byte> data, IPEndPoint remote)
    {
        try
        {
            await _udpClient.SendAsync(data.ToArray(), data.Length, remote);
        }
        catch (Exception ex)
        {
            _pipeline.FireExceptionCaught(ex);
        }
    }

    public void Dispose()
    {
        Stop();
        _udpClient.Dispose();
        _cts.Dispose();
    }
}
