using System;
using System.Buffers.Binary;
using System.Text;

namespace Network.Rcon;

public static class RconCodec
{
    public const int HeaderSize = 4;
    public const int MinimumFrameSize = 10;

    public static byte[] Encode(RconMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);

        byte[] body = Encoding.UTF8.GetBytes(message.Body ?? string.Empty);
        int frameSize = body.Length + 10;
        byte[] buffer = new byte[HeaderSize + frameSize];

        BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(0, HeaderSize), frameSize);
        BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(HeaderSize, 4), message.Id);
        BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(HeaderSize + 4, 4), message.Type);

        body.CopyTo(buffer.AsSpan(HeaderSize + 8, body.Length));
        buffer[^2] = 0;
        buffer[^1] = 0;
        return buffer;
    }

    public static bool TryDecode(ReadOnlySpan<byte> buffer, out RconMessage? message, out int consumed)
    {
        message = null;
        consumed = 0;

        if (buffer.Length < HeaderSize)
        {
            return false;
        }

        int frameSize = BinaryPrimitives.ReadInt32LittleEndian(buffer.Slice(0, HeaderSize));
        if (frameSize < MinimumFrameSize)
        {
            return false;
        }

        int totalLength = HeaderSize + frameSize;
        if (buffer.Length < totalLength)
        {
            return false;
        }

        ReadOnlySpan<byte> payload = buffer.Slice(HeaderSize, frameSize);
        if (payload.Length < MinimumFrameSize)
        {
            return false;
        }

        int id = BinaryPrimitives.ReadInt32LittleEndian(payload.Slice(0, 4));
        int type = BinaryPrimitives.ReadInt32LittleEndian(payload.Slice(4, 4));

        int bodyLength = frameSize - 10;
        ReadOnlySpan<byte> bodyBytes = payload.Slice(8, bodyLength);
        string body = Encoding.UTF8.GetString(bodyBytes).TrimEnd('\0');

        message = new RconMessage(id, type, body);
        consumed = totalLength;
        return true;
    }
}