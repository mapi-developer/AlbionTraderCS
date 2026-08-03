using System.Collections.Generic;
using System.IO;
using AlbionBot.Core.Enums;
using AlbionBot.Core.Models;
using AlbionBot.Network.Parsers; // Needs access to BinaryReaderExtensions

namespace AlbionBot.Network.Buffers;

public class FragmentBuffer
{
    private readonly Dictionary<int, Dictionary<int, byte[]>> _buffers = new();
    private readonly Dictionary<int, int> _fragmentCounts = new();
    private readonly object _lockObj = new(); // Ensures thread safety

    public PhotonCommand? Offer(PhotonCommand cmd)
    {
        using var ms = new MemoryStream(cmd.Data);
        using var reader = new BinaryReader(ms);

        // Read Fragment Header (12 bytes)
        int sequenceNumber = reader.ReadBigEndianInt32();
        int fragmentCount = reader.ReadBigEndianInt32();
        int fragmentNumber = reader.ReadBigEndianInt32();
        int totalLength = reader.ReadBigEndianInt32(); 
        int fragmentOffset = reader.ReadBigEndianInt32();
        
        byte[] fragmentData = reader.ReadBytes((int)(ms.Length - ms.Position));

        // Lock dictionary to prevent race conditions during rapid packet arrival
        lock (_lockObj)
        {
            if (!_buffers.ContainsKey(sequenceNumber))
            {
                _buffers[sequenceNumber] = new Dictionary<int, byte[]>();
                _fragmentCounts[sequenceNumber] = fragmentCount;
            }

            _buffers[sequenceNumber][fragmentNumber] = fragmentData;

            if (_buffers[sequenceNumber].Count == fragmentCount)
            {
                return Reassemble(sequenceNumber);
            }
        }

        return null;
    }

    private PhotonCommand Reassemble(int sequenceNumber)
    {
        var parts = _buffers[sequenceNumber];
        int totalFragments = _fragmentCounts[sequenceNumber];
        
        using var ms = new MemoryStream();
        for (int i = 0; i < totalFragments; i++)
        {
            if (parts.TryGetValue(i, out byte[]? part))
            {
                ms.Write(part, 0, part.Length);
            }
        }

        // Clean up memory
        _buffers.Remove(sequenceNumber);
        _fragmentCounts.Remove(sequenceNumber);

        return new PhotonCommand
        {
            Type = CommandType.SendReliableType,
            Data = ms.ToArray()
        };
    }
}