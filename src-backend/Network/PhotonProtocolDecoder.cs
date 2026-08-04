using System;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using AlbionBot.Infrastructure;

namespace AlbionBot.Network;

public class PhotonPacket
{
    public int PeerId { get; init; }
    public byte Flags { get; init; }
    public byte CommandCount { get; init; }
    public uint Timestamp { get; init; }
    public uint SequenceNumber { get; init; }
    public byte CommandType { get; init; }
    public ReadOnlyMemory<byte> Payload { get; init; } = ReadOnlyMemory<byte>.Empty;
}

public class PhotonProtocolDecoder
{
    private readonly ConcurrentDictionary<int, FragmentBuffer> _fragmentBuffers = new();

    public IEnumerable<PhotonPacket> DecodeRawPayload(ReadOnlyMemory<byte> payload)
    {
        using var ms = new MemoryStream(payload.ToArray());
        using var reader = new BinaryReader(ms);

        if (ms.Length - ms.Position < 12) yield break; // Photon packet header is 12 bytes

        var peerId = BinaryPrimitives.ReadUInt16BigEndian(reader.ReadBytes(2));
        var flags = reader.ReadByte();
        var commandCount = reader.ReadByte();
        var timestamp = BinaryPrimitives.ReadUInt32BigEndian(reader.ReadBytes(4));
        var sequenceNumber = BinaryPrimitives.ReadUInt32BigEndian(reader.ReadBytes(4));

        for (var i = 0; i < commandCount; i++)
        {
            if (ms.Length - ms.Position < 12) yield break; // Photon command header is 12 bytes

            var commandType = reader.ReadByte();
            reader.ReadBytes(3); // Skip ChannelId, Flags, Reserved
            var commandLength = BinaryPrimitives.ReadInt32BigEndian(reader.ReadBytes(4));
            var reliableSequenceNumber = BinaryPrimitives.ReadInt32BigEndian(reader.ReadBytes(4));

            // commandLength includes the 12-byte header, so the data payload is commandLength - 12
            var dataLength = commandLength - 12;

            if (dataLength < 0 || ms.Position + dataLength > ms.Length)
            {
                DebugLogger.Log($"Invalid Photon data length={dataLength}, available={ms.Length - ms.Position}");
                yield break;
            }

            var commandData = reader.ReadBytes(dataLength);

            var packet = new PhotonPacket
            {
                PeerId = peerId,
                Flags = flags,
                CommandCount = commandCount,
                Timestamp = timestamp,
                SequenceNumber = sequenceNumber,
                CommandType = commandType,
                Payload = commandData
            };

            switch (commandType)
            {
                case 0x06: // SendReliable
                case 0x07: // SendUnreliable
                    yield return packet;
                    break;
                case 0x08: // SendReliableFragment
                    var fragment = DecodeFragment(commandData);
                    if (fragment != null)
                    {
                        yield return fragment;
                    }
                    break;
            }
        }
    }

    private PhotonPacket? DecodeFragment(byte[] fragmentData)
    {
        if (fragmentData.Length < 20) return null; // Fragment header is 20 bytes (5x 32-bit ints)

        using var ms = new MemoryStream(fragmentData);
        using var reader = new BinaryReader(ms);

        var sequenceNumber = BinaryPrimitives.ReadUInt32BigEndian(reader.ReadBytes(4));
        var fragmentCount = BinaryPrimitives.ReadUInt32BigEndian(reader.ReadBytes(4));
        var fragmentNumber = BinaryPrimitives.ReadUInt32BigEndian(reader.ReadBytes(4));
        var totalLength = BinaryPrimitives.ReadUInt32BigEndian(reader.ReadBytes(4));
        var fragmentOffset = BinaryPrimitives.ReadUInt32BigEndian(reader.ReadBytes(4));

        var payload = reader.ReadBytes((int)(ms.Length - ms.Position));

        var buffer = _fragmentBuffers.GetOrAdd((int)sequenceNumber, _ => new FragmentBuffer(totalLength, fragmentCount));
        
        // Albion fragments must be written by their explicit offset, not by an index multiplier
        buffer.AddSegment((int)fragmentOffset, payload);

        if (buffer.IsComplete)
        {
            _fragmentBuffers.TryRemove((int)sequenceNumber, out _);
            DebugLogger.Log($"Fragment sequence {sequenceNumber} complete, reassembling.");
            
            return new PhotonPacket 
            { 
                CommandType = 0x06, // Reassembled packets are treated as standard SendReliable packets 
                Payload = buffer.Reassemble() 
            };
        }

        return null;
    }

    private sealed class FragmentBuffer
    {
        private readonly byte[] _data;
        private int _fragmentsReceived;
        private readonly int _totalFragments;

        public FragmentBuffer(uint totalLength, uint segmentCount)
        {
            _data = new byte[totalLength];
            _totalFragments = (int)segmentCount;
            _fragmentsReceived = 0;
        }

        public void AddSegment(int offset, byte[] segmentData)
        {
            if (offset >= 0 && offset + segmentData.Length <= _data.Length)
            {
                Buffer.BlockCopy(segmentData, 0, _data, offset, segmentData.Length);
                _fragmentsReceived++;
            }
        }

        public bool IsComplete => _fragmentsReceived >= _totalFragments;

        public ReadOnlyMemory<byte> Reassemble() => _data;
    }
}