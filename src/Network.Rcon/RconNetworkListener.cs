using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Network.Rcon;

public sealed class RconNetworkListener : IDisposable
{
    private readonly TcpListener _listener;
    private readonly CancellationTokenSource _cts = new();
    private readonly Action<RconMessage> _onMessage;
    private readonly Func<int, string, bool>? _onAuth;

    public RconNetworkListener(string host, int port, Action<RconMessage> onMessage, Func<int, string, bool>? onAuth = null)
    {
        _listener = new TcpListener(IPAddress.Parse(host), port);
        _onMessage = onMessage ?? throw new ArgumentNullException(nameof(onMessage));
        _onAuth = onAuth;
    }

    public void Start()
    {
        _listener.Start();
        _ = Task.Run(() => AcceptLoopAsync(_cts.Token));
    }

    public void Stop()
    {
        _cts.Cancel();
        _listener.Stop();
    }

    private async Task AcceptLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            TcpClient client;
            try
            {
                client = await _listener.AcceptTcpClientAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (SocketException)
            {
                continue;
            }

            _ = Task.Run(() => HandleClientAsync(client, cancellationToken));
        }
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken cancellationToken)
    {
        using var stream = client.GetStream();
        using var memory = new MemoryStream();

        byte[] buffer = new byte[4096];
        while (!cancellationToken.IsCancellationRequested && client.Connected)
        {
            int read;
            try
            {
                read = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (IOException)
            {
                break;
            }

            if (read <= 0)
            {
                break;
            }

            memory.Write(buffer, 0, read);

            while (RconCodec.TryDecode(memory.ToArray(), out var packet, out int consumed))
            {
                if (packet is not null)
                {
                    _onMessage(packet);
                }

                byte[] remaining = memory.ToArray();
                if (consumed >= remaining.Length)
                {
                    memory.SetLength(0);
                    break;
                }

                memory.Position = 0;
                memory.SetLength(remaining.Length - consumed);
                Array.Copy(remaining, consumed, memory.GetBuffer(), 0, memory.Length);
                memory.Position = memory.Length;
            }
        }
    }

    public void Dispose()
    {
        Stop();
        _listener.Dispose();
        _cts.Dispose();
    }
}