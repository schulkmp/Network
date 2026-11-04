// Deprecated: this file is intentionally left as a thin compatibility wrapper.
using System;
using System.Net;
using Network.Raknet.Channel.Raknet.Packet;

namespace Network.Raknet.Handler.Codec.Raknet.Common;

public static class RakDatagramCodec
{
    [Obsolete("Use RakNetCodec instead - RakDatagramCodec is a simple passthrough wrapper.")]
    public static bool TryDecode(ReadOnlySpan<byte> buffer, IPEndPoint? remoteEndPoint, out EncapsulatedPacket? packet)
    {
        packet = null;
        if (buffer.IsEmpty) return false;

        packet = new EncapsulatedPacket(buffer.ToArray());
        return true;
    }

    [Obsolete("Use RakNetCodec.Encode")]
    public static byte[] Encode(EncapsulatedPacket packet)
    {
        if (packet is null) throw new ArgumentNullException(nameof(packet));
        return packet.Payload;
    }
}