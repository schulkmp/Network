namespace Network.Raknet.Channel.Raknet.Packet;

using System.Net;

public sealed class RakDatagramPacket
{
    public RakDatagramPacket(IPEndPoint remoteEndPoint, byte[] payload)
    {
        RemoteEndPoint = remoteEndPoint;
        Payload = payload;
    }

    public IPEndPoint RemoteEndPoint { get; }
    public byte[] Payload { get; }

    public override string ToString() => $"RakDatagramPacket(From={RemoteEndPoint}, Length={Payload?.Length ?? 0})";
}