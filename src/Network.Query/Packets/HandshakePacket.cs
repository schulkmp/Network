using System;
using System.Text;
using Network.Query;

namespace Network.Query.Packets;

public sealed class HandshakePacket : QueryPacket
{
    public HandshakePacket(int sessionId, string serverName, string levelName, int protocolVersion, int version, int maxPlayers, int onlinePlayers)
        : base(QueryPacketType.Handshake, sessionId, CreatePayload(serverName, levelName, protocolVersion, version, maxPlayers, onlinePlayers))
    {
        ServerName = serverName ?? throw new ArgumentNullException(nameof(serverName));
        LevelName = levelName ?? throw new ArgumentNullException(nameof(levelName));
        ProtocolVersion = protocolVersion;
        Version = version;
        MaxPlayers = maxPlayers;
        OnlinePlayers = onlinePlayers;
    }

    public string ServerName { get; }
    public string LevelName { get; }
    public int ProtocolVersion { get; }
    public int Version { get; }
    public int MaxPlayers { get; }
    public int OnlinePlayers { get; }

    private static byte[] CreatePayload(string serverName, string levelName, int protocolVersion, int version, int maxPlayers, int onlinePlayers)
    {
        var sb = new StringBuilder();
        sb.Append("server=").Append(serverName).Append(';');
        sb.Append("level=").Append(levelName).Append(';');
        sb.Append("protocol=").Append(protocolVersion).Append(';');
        sb.Append("version=").Append(version).Append(';');
        sb.Append("maxPlayers=").Append(maxPlayers).Append(';');
        sb.Append("onlinePlayers=").Append(onlinePlayers).Append(';');
        return Encoding.UTF8.GetBytes(sb.ToString());
    }
}