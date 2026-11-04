namespace Network.Raknet.Channel.Raknet;

using System;
using System.Net;
using System.Threading.Tasks;

public sealed class RakServerChannel : RakChannel
{
    private readonly Func<ReadOnlyMemory<byte>, IPEndPoint, ValueTask> _writer;
    private int _nextMessageIndex = 1;
    private readonly System.Collections.Concurrent.ConcurrentDictionary<uint, (byte[][] Fragments, DateTimeOffset SentAt, int Retries)> _outstanding = new();
    private readonly System.Threading.Timer? _retransmitTimer;
    public RakServerChannel(IPEndPoint remoteEndPoint, Func<ReadOnlyMemory<byte>, IPEndPoint, ValueTask> writer)
    {
        RemoteEndPoint = remoteEndPoint ?? throw new ArgumentNullException(nameof(remoteEndPoint));
        _writer = writer ?? throw new ArgumentNullException(nameof(writer));
        Pipeline = new RakChannelPipeline();
        SlidingWindow = new RakSlidingWindow();
        _retransmitTimer = new System.Threading.Timer(OnRetransmitTimer, null, 500, 500);
    }

    public IPEndPoint RemoteEndPoint { get; }

    public RakChannelPipeline Pipeline { get; }

    public RakSlidingWindow SlidingWindow { get; }

    public ValueTask WriteAsync(ReadOnlyMemory<byte> data) => _writer(data, RemoteEndPoint);

    public ValueTask CloseAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }

    public void Dispose()
    {
        // notify pipeline about channel inactive
        Pipeline.FireChannelInactive();
        _retransmitTimer?.Dispose();
    }

    private void OnRetransmitTimer(object? state)
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var kv in _outstanding)
        {
            var seq = kv.Key;
            var entry = kv.Value;
            if ((now - entry.SentAt).TotalMilliseconds > 500 && entry.Retries < 5)
            {
                // resend all fragments
                foreach (var frag in entry.Fragments)
                {
                    _ = _writer(frag, RemoteEndPoint);
                }
                _outstanding[seq] = (entry.Fragments, DateTimeOffset.UtcNow, entry.Retries + 1);
            }
        }
    }

    /// <summary>
    /// Send a payload reliably (store in outstanding and send with message index).
    /// </summary>
    public async ValueTask<uint> SendReliableAsync(ReadOnlyMemory<byte> payload)
    {
        var bytes = payload.ToArray();
        var seqInt = System.Threading.Interlocked.Increment(ref _nextMessageIndex);
        if (seqInt == 0) seqInt = System.Threading.Interlocked.Increment(ref _nextMessageIndex); // avoid zero
        uint seq = (uint)seqInt;

        int maxFrag = Channel.Raknet.RakConstants.DefaultMaxFragmentPayload;
        byte flags = 0x20; // reliable flag set (example)

        byte[][] fragments;
        if (bytes.Length <= maxFrag)
        {
            var buffer = Network.Raknet.Handler.Codec.Raknet.Common.RakNetCodec.EncodeReliable(seq, flags, bytes);
            fragments = new[] { buffer };
        }
        else
        {
            fragments = Network.Raknet.Handler.Codec.Raknet.Common.RakNetCodec.EncodeSplitReliable(seq, flags, bytes, maxFrag);
        }

        _outstanding[seq] = (fragments, DateTimeOffset.UtcNow, 0);
        foreach (var frag in fragments)
        {
            await _writer(frag, RemoteEndPoint);
        }

        return seq;
    }

    /// <summary>
    /// Process incoming ACKs (sequence numbers) and remove outstanding entries.
    /// </summary>
    public void ProcessAcks(uint[] sequences)
    {
        foreach (var s in sequences)
        {
            _outstanding.TryRemove(s, out _);
        }
    }

    public int OutstandingCount => _outstanding.Count;
}