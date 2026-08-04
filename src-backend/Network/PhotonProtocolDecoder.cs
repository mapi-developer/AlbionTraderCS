using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using AlbionBot.Protocol;

namespace AlbionBot.Network;

public class PhotonPacket
{
    public int PeerId { get; init; }
    public byte Flags { get; init; }
    public byte CommandCount { get; init; }
    public uint Timestamp { get; init; }
    public uint SequenceNumber { get; init; }
    public ReadOnlyMemory<byte> Payload { get; init; } = ReadOnlyMemory<byte>.Empty;
}

public class PhotonProtocolDecoder
{
    private readonly ConcurrentDictionary<int, FragmentBuffer> _fragmentBuffers = new();

    public IEnumerable<PhotonPacket> DecodeRawPayload(ReadOnlyMemory<byte> payload)
    {
        var reader = new BinaryReader(new MemoryStream(payload.ToArray()));

        var peerId = reader.ReadUInt16();
        var flags = reader.ReadByte();
        var commandCount = reader.ReadByte();
        var timestamp = reader.ReadUInt32();
        var sequenceNumber = reader.ReadUInt32();

        var unpacked = new List<PhotonPacket>();
        for (var i = 0; i < commandCount; i++)
        {
            if (reader.BaseStream.Position >= reader.BaseStream.Length)
            {
                break;
            }

            var commandType = reader.ReadByte();
            var commandSize = reader.ReadUInt16();
            var commandData = reader.ReadBytes(commandSize);
            var packet = new PhotonPacket
            {
                PeerId = peerId,
                Flags = flags,
                CommandCount = commandCount,
                Timestamp = timestamp,
                SequenceNumber = sequenceNumber,
                Payload = commandData
            };

            switch (commandType)
            {
                case 0x06:
                case 0x07:
                    unpacked.Add(packet);
                    break;
                case 0x08:
                    var fragment = DecodeFragment(commandData);
                    if (fragment != null)
                    {
                        unpacked.Add(fragment);
                    }
                    break;
                default:
                    // Unknown command type, ignore.
                    break;
            }
        }

        return unpacked;
    }

    private PhotonPacket? DecodeFragment(byte[] fragmentData)
    {
        using var reader = new BinaryReader(new MemoryStream(fragmentData));
        var fragmentId = reader.ReadUInt16();
        var fragmentCount = reader.ReadUInt16();
        var fragmentNumber = reader.ReadUInt16();
        var totalLength = reader.ReadUInt32();
        var payloadLength = reader.ReadUInt16();
        var payload = reader.ReadBytes(payloadLength);

        var buffer = _fragmentBuffers.GetOrAdd(fragmentId, _ => new FragmentBuffer(totalLength, fragmentCount));
        buffer.AddSegment(fragmentNumber, payload);

        if (buffer.IsComplete)
        {
            _fragmentBuffers.TryRemove(fragmentId, out _);
            return new PhotonPacket { Payload = buffer.Reassemble() };
        }

        return null;
    }

    private sealed class FragmentBuffer
    {
        private readonly byte[] _data;
        private readonly bool[] _received;
        private readonly int _segmentCount;

        public FragmentBuffer(uint totalLength, uint segmentCount)
        {
            _data = new byte[totalLength];
            _segmentCount = (int)segmentCount;
            _received = new bool[_segmentCount];
        }

        public void AddSegment(int segmentIndex, byte[] segmentData)
        {
            var offset = segmentIndex * segmentData.Length;
            Buffer.BlockCopy(segmentData, 0, _data, offset, segmentData.Length);
            if (segmentIndex >= 0 && segmentIndex < _segmentCount)
            {
                _received[segmentIndex] = true;
            }
        }

        public bool IsComplete => Array.TrueForAll(_received, value => value);

        public ReadOnlyMemory<byte> Reassemble() => _data;
    }
}
