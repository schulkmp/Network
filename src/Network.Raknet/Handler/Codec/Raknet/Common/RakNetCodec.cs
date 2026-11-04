using System;
using System.Buffers.Binary;
using System.Net;
using Network.Raknet.Channel.Raknet.Packet;
using Network.Raknet.Util;

namespace Network.Raknet.Handler.Codec.Raknet.Common;

public sealed class RakNetFrame
{
    public byte Flags { get; init; }
    public byte Reliability { get; init; }
    public uint? MessageIndex { get; init; }
    public uint? OrderingIndex { get; init; }
    public byte? OrderingChannel { get; init; }
    public int? SplitId { get; init; }
    public ushort? SplitCount { get; init; }
    public ushort? SplitIndex { get; init; }
}

public static class RakNetCodec
{
    private const byte SplitFlag = 0x10;
    private const byte ReliabilityMask = 0xE0; // top 3 bits

    /// <summary>
    /// Try to decode a buffer into an encapsulated payload and optional frame metadata.
    /// This parser is RakNet-inspired and supports a common subset used by Bedrock: flags, message index,
    /// ordering index/channel, and split fragments.
    /// </summary>
    public static bool TryDecode(ReadOnlySpan<byte> buffer, IPEndPoint? remoteEndPoint, out EncapsulatedPacket? packet, out RakNetFrame? frame)
    {
        packet = null;
        frame = null;
        if (buffer.IsEmpty) return false;

        byte flags = buffer[0];
        byte reliability = (byte)((flags & ReliabilityMask) >> 5);
        bool isSplit = (flags & SplitFlag) != 0;

        int cursor = 1;

        uint? messageIndex = null;
        uint? orderingIndex = null;
        byte? orderingChannel = null;

        // Many RakNet reliabilities include a 3-byte message index; we'll try to parse if enough bytes
        if (buffer.Length >= cursor + 3)
        {
            messageIndex = (uint)((buffer[cursor] << 16) | (buffer[cursor + 1] << 8) | buffer[cursor + 2]);
            cursor += 3;
        }

        // ordering index (3 bytes) + channel (1 byte) if present
        if (buffer.Length >= cursor + 4)
        {
            orderingIndex = (uint)((buffer[cursor] << 16) | (buffer[cursor + 1] << 8) | buffer[cursor + 2]);
            orderingChannel = buffer[cursor + 3];
            cursor += 4;
        }

        if (!isSplit)
        {
            if (buffer.Length <= cursor) return false;
            frame = new RakNetFrame
            {
                Flags = flags,
                Reliability = reliability,
                MessageIndex = messageIndex,
                OrderingIndex = orderingIndex,
                OrderingChannel = orderingChannel
            };

            packet = new EncapsulatedPacket(buffer.Slice(cursor).ToArray());
            return true;
        }

        // split header requires at least 8 bytes for split metadata
        if (buffer.Length < cursor + 8) return false;

        int splitId = BinaryPrimitives.ReadInt32LittleEndian(buffer.Slice(cursor, 4));
        ushort totalParts = BinaryPrimitives.ReadUInt16LittleEndian(buffer.Slice(cursor + 4, 2));
        ushort index = BinaryPrimitives.ReadUInt16LittleEndian(buffer.Slice(cursor + 6, 2));
        cursor += 8;

        var fragment = buffer.Slice(cursor).ToArray();

        string key = remoteEndPoint is null ? "local" : remoteEndPoint.ToString();
        var assembled = SplitPacketHelper.AddFragment(key, splitId, totalParts, index, fragment);
        if (assembled is null)
        {
            // not complete yet, but we still expose frame metadata
            frame = new RakNetFrame
            {
                Flags = flags,
                Reliability = reliability,
                MessageIndex = messageIndex,
                OrderingIndex = orderingIndex,
                OrderingChannel = orderingChannel,
                SplitId = splitId,
                SplitCount = totalParts,
                SplitIndex = index
            };

            return false;
        }

        frame = new RakNetFrame
        {
            Flags = flags,
            Reliability = reliability,
            MessageIndex = messageIndex,
            OrderingIndex = orderingIndex,
            OrderingChannel = orderingChannel,
            SplitId = splitId,
            SplitCount = totalParts,
            SplitIndex = index
        };

        packet = new EncapsulatedPacket(assembled);
        return true;
    }

    // Backwards-compatible overload
    public static bool TryDecode(ReadOnlySpan<byte> buffer, IPEndPoint? remoteEndPoint, out EncapsulatedPacket? packet)
    {
        return TryDecode(buffer, remoteEndPoint, out packet, out _);
    }

    public static byte[] EncodeFrame(byte flags, byte[] payload)
    {
        if (payload is null) throw new ArgumentNullException(nameof(payload));
        var buffer = new byte[1 + payload.Length];
        buffer[0] = flags;
        Buffer.BlockCopy(payload, 0, buffer, 1, payload.Length);
        return buffer;
    }

    /// <summary>
    /// Encode a reliable frame including a 3-byte message index (big-endian in 3 bytes as used by parser).
    /// Layout: [flags(1)] [messageIndex(3)] [payload...]
    /// </summary>
    public static byte[] EncodeReliable(uint messageIndex, byte flags, byte[] payload)
    {
        if (payload is null) throw new ArgumentNullException(nameof(payload));
        var buffer = new byte[1 + 3 + payload.Length];
        buffer[0] = flags;
        // messageIndex in 3 bytes big-endian
        buffer[1] = (byte)((messageIndex >> 16) & 0xFF);
        buffer[2] = (byte)((messageIndex >> 8) & 0xFF);
        buffer[3] = (byte)(messageIndex & 0xFF);
        Buffer.BlockCopy(payload, 0, buffer, 4, payload.Length);
        return buffer;
    }

    /// <summary>
    /// Encode a reliable fragmented message. Splits payload into fragments each with split header.
    /// Returns an array of encoded fragment datagrams ready to send.
    /// Fragment header layout after messageIndex/or ordering fields used by TryDecode:
    /// [splitId(int32 LE)][totalParts(ushort LE)][index(ushort LE)][fragmentPayload]
    /// The flags byte will include the SplitFlag.
    /// </summary>
    public static byte[][] EncodeSplitReliable(uint messageIndex, byte flags, byte[] payload, int maxFragmentPayload)
    {
        if (payload is null) throw new ArgumentNullException(nameof(payload));
        if (maxFragmentPayload <= 0) throw new ArgumentOutOfRangeException(nameof(maxFragmentPayload));

        // compute how many fragments
        int total = (payload.Length + maxFragmentPayload - 1) / maxFragmentPayload;
        var splitId = System.Environment.TickCount & int.MaxValue;
        var fragments = new byte[total][];

        for (int i = 0; i < total; i++)
        {
            int offset = i * maxFragmentPayload;
            int len = System.Math.Min(maxFragmentPayload, payload.Length - offset);

            // build header: flags + messageIndex(3) + split metadata(8) + fragment payload
            var frag = new byte[1 + 3 + 8 + len];
            byte splitFlags = (byte)(flags | SplitFlag);
            frag[0] = splitFlags;

            // messageIndex 3 bytes big-endian
            frag[1] = (byte)((messageIndex >> 16) & 0xFF);
            frag[2] = (byte)((messageIndex >> 8) & 0xFF);
            frag[3] = (byte)(messageIndex & 0xFF);

            // split metadata (little-endian)
            BinaryPrimitives.WriteInt32LittleEndian(frag.AsSpan(4, 4), splitId);
            BinaryPrimitives.WriteUInt16LittleEndian(frag.AsSpan(8, 2), (ushort)total);
            BinaryPrimitives.WriteUInt16LittleEndian(frag.AsSpan(10, 2), (ushort)i);

            Buffer.BlockCopy(payload, offset, frag, 12, len);
            fragments[i] = frag;
        }

        return fragments;
    }
}
