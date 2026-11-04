namespace Network.Raknet.Channel.Raknet.Packet;

public sealed class EncapsulatedPacket
{
    public EncapsulatedPacket(byte[] payload)
    {
        Payload = payload;
    }

    public byte[] Payload { get; }

    public override string ToString() => $"EncapsulatedPacket(Length={Payload?.Length ?? 0})";
}