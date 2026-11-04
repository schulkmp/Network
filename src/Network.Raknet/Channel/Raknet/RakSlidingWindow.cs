namespace Network.Raknet.Channel.Raknet;

using System.Collections.Concurrent;
using System.Collections.Generic;

/// <summary>
/// Simple sliding window tracker for received sequence numbers.
/// This is a lightweight implementation used to gather ACKs for received packets.
/// Not a full RFC-compliant sliding window; suitable for initial reliability handling.
/// </summary>
public sealed class RakSlidingWindow
{
    private readonly ConcurrentDictionary<uint, bool> _received = new();
    private readonly ConcurrentQueue<uint> _pendingAcks = new();

    public RakSlidingWindow()
    {
    }

    /// <summary>
    /// Mark a sequence as received. Returns true if it was newly accepted (not a duplicate).
    /// </summary>
    public bool Accept(uint sequence)
    {
        if (_received.TryAdd(sequence, true))
        {
            _pendingAcks.Enqueue(sequence);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Take pending ACKs snapshot and clear the pending queue.
    /// </summary>
    public uint[] TakePendingAcks()
    {
        var list = new List<uint>();
        while (_pendingAcks.TryDequeue(out var seq)) list.Add(seq);
        return list.ToArray();
    }
}