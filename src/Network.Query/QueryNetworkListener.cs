using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Network.Query.Codec;

namespace Network.Query;

public sealed class QueryNetworkListener : IDisposable
{
    private readonly UdpClient _udpClient;
    private readonly CancellationTokenSource _cts = new();
    private readonly Action<QueryPacket> _onPacket;
    private readonly Func<QueryPacket, bool>? _filter;

    public QueryNetworkListener(string host, int port, Action<QueryPacket> onPacket, Func<QueryPacket, bool>? filter = null)
    {
        _udpClient = new UdpClient(new IPEndPoint(IPAddress.Parse(host), port));
        _onPacket = onPacket ?? throw new ArgumentNullException(nameof(onPacket));
        _filter = filter;
    }

    public void Start()
    {
        _ = Task.Run(() => ReceiveLoopAsync(_cts.Token));
    }

    public void Stop()
    {
        _cts.Cancel();
        _udpClient.Close();
    }

    private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            UdpReceiveResult result;
            try
            {
                result = await _udpClient.ReceiveAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (SocketException)
            {
                continue;
            }

            if (result.Buffer.Length < 8)
            {
                continue;
            }

            if (!QueryPacketCodec.TryDecode(result.Buffer, out var packet) || packet is null)
            {
                continue;
            }

            if (_filter is not null && !_filter(packet))
            {
                continue;
            }

            _onPacket(packet);
        }
    }

    public void Dispose()
    {
        Stop();
        _udpClient.Dispose();
        _cts.Dispose();
    }
}