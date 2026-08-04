using System;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using AlbionBot.Infrastructure;
using AlbionBot.Protocol;

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
        DebugLogger.Log($"Decoding raw payload length={payload.Length}");
        using var reader = new BinaryReader(new MemoryStream(payload.ToArray()));

        if (reader.BaseStream.Length - reader.BaseStream.Position < 12)
        {
            DebugLogger.Log("Photon payload too short for header, skipping.");
            yield break;
        }

        var peerId = reader.ReadUInt16();
        var flags = reader.ReadByte();
        var commandCount = reader.ReadByte();
        var timestamp = reader.ReadUInt32();
        var sequenceNumber = reader.ReadUInt32();

        for (var i = 0; i < commandCount; i++)
        {
            if (reader.BaseStream.Length - reader.BaseStream.Position < 3)
            {
                DebugLogger.Log("Photon command header truncated, stopping decode loop.");
                yield break;
            }

            var commandType = reader.ReadByte();
            var commandSize = BinaryPrimitives.ReadUInt16BigEndian(reader.ReadBytes(2));
            DebugLogger.Log($"Photon command {i + 1}/{commandCount}: type=0x{commandType:X2}, size={commandSize}");

            if (commandSize == 0)
            {
                DebugLogger.Log($"Photon command type=0x{commandType:X2} has zero length, skipping.");
                continue;
            }

            if (reader.BaseStream.Length - reader.BaseStream.Position < commandSize)
            {
                DebugLogger.Log($"Invalid Photon command size={commandSize}, available={reader.BaseStream.Length - reader.BaseStream.Position}");
                yield break;
            }

            var commandData = reader.ReadBytes(commandSize);
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
                case 0x06:
                case 0x07:
                    DebugLogger.Log($"Photon payload yielded commandType=0x{commandType:X2}");
                    yield return packet;
                    break;
                case 0x08:
                    DebugLogger.Log("Photon payload contains fragment packet.");
                    var fragment = DecodeFragment(commandData);
                    if (fragment != null)
                    {
                        DebugLogger.Log("Fragment reassembled into complete payload.");
                        yield return fragment;
                    }
                    else
                    {
                        DebugLogger.Log("Fragment stored, awaiting remaining segments.");
                    }
                    break;
                default:
                    DebugLogger.Log($"Skipping unsupported Photon commandType=0x{commandType:X2}");
                    break;
            }
        }
    }

    private PhotonPacket? DecodeFragment(byte[] fragmentData)
    {
        if (fragmentData.Length < 14)
        {
            DebugLogger.Log($"Invalid fragment packet length={fragmentData.Length}");
            return null;
        }

        using var reader = new BinaryReader(new MemoryStream(fragmentData));
        var fragmentId = reader.ReadUInt16();
        var fragmentCount = reader.ReadUInt16();
        var fragmentNumber = reader.ReadUInt16();
        var totalLength = reader.ReadUInt32();
        var payloadLength = reader.ReadUInt16();

        if (fragmentData.Length < 14 + payloadLength)
        {
            DebugLogger.Log($"Fragment payload length mismatch: expected={payloadLength}, available={fragmentData.Length - 14}");
            return null;
        }

        var payload = reader.ReadBytes(payloadLength);
        DebugLogger.Log($"Fragment packet id={fragmentId} index={fragmentNumber}/{fragmentCount} totalLength={totalLength} payloadLength={payloadLength}");

        var buffer = _fragmentBuffers.GetOrAdd(fragmentId, _ => new FragmentBuffer(totalLength, fragmentCount));
        buffer.AddSegment(fragmentNumber, payload);

        if (buffer.IsComplete)
        {
            _fragmentBuffers.TryRemove(fragmentId, out _);
            DebugLogger.Log($"Fragment id={fragmentId} complete, reassembling.");
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
