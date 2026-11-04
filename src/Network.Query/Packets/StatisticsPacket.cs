using System;
using System.Collections.Generic;
using System.Text;
using Network.Query;

namespace Network.Query.Packets;

public sealed class StatisticsPacket : QueryPacket
{
    public StatisticsPacket(int sessionId, IReadOnlyDictionary<string, string> values)
        : base(QueryPacketType.Statistics, sessionId, CreatePayload(values))
    {
        Values = new Dictionary<string, string>(values ?? throw new ArgumentNullException(nameof(values)), StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyDictionary<string, string> Values { get; }

    private static byte[] CreatePayload(IReadOnlyDictionary<string, string> values)
    {
        var sb = new StringBuilder();
        foreach (var kvp in values)
        {
            sb.Append(kvp.Key).Append('=').Append(kvp.Value).Append(';');
        }

        return Encoding.UTF8.GetBytes(sb.ToString());
    }
}