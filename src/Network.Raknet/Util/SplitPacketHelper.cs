using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace Network.Raknet.Util;

/// <summary>
/// Small helper to reassemble split fragments. This is intentionally simple and
/// only supports the internal split header format used by our codec.
/// </summary>
public static class SplitPacketHelper
{
    private sealed class Assembly
    {
        public int TotalParts;
        public byte[][] Parts;
        public DateTimeOffset LastUpdated = DateTimeOffset.UtcNow;
    }

    // key: remoteEndpoint + ':' + splitId
    private static readonly ConcurrentDictionary<string, Assembly> Assemblies = new();

    public static byte[]? AddFragment(string key, int splitId, int totalParts, int index, byte[] fragment)
    {
        string dictKey = key + ":" + splitId;

        var assembly = Assemblies.GetOrAdd(dictKey, _ => new Assembly { TotalParts = totalParts, Parts = new byte[totalParts][] });

        lock (assembly)
        {
            assembly.Parts[index] = fragment;
            assembly.LastUpdated = DateTimeOffset.UtcNow;

            // check if all parts present
            for (int i = 0; i < assembly.TotalParts; i++)
            {
                if (assembly.Parts[i] is null) return null;
            }

            // assemble
            int totalLength = assembly.Parts.Sum(p => p.Length);
            var buffer = new byte[totalLength];
            int pos = 0;
            for (int i = 0; i < assembly.TotalParts; i++)
            {
                Buffer.BlockCopy(assembly.Parts[i], 0, buffer, pos, assembly.Parts[i].Length);
                pos += assembly.Parts[i].Length;
            }

            Assemblies.TryRemove(dictKey, out _);
            return buffer;
        }
    }

    // Periodic cleanup to remove stale assemblies
    public static void Cleanup(TimeSpan olderThan)
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var kv in Assemblies)
        {
            if (now - kv.Value.LastUpdated > olderThan)
            {
                Assemblies.TryRemove(kv.Key, out _);
            }
        }
    }
}