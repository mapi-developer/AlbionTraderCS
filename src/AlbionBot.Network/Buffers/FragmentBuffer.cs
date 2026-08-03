using System;
using System.Collections.Generic;
using System.IO;
using AlbionBot.Core.Enums;
using AlbionBot.Core.Models;
using AlbionBot.Network.Parsers;

namespace AlbionBot.Network.Buffers;

public class FragmentBuffer
{
    private class FragmentSequence
    {
        public int FragmentsReceived { get; set; }
        public uint TotalFragments { get; set; }
        public byte[] Buffer { get; set; } = [];
    }

    private readonly Dictionary<uint, FragmentSequence> _sequences = new();
    private readonly object _lockObj = new();

    public PhotonCommand? Offer(PhotonCommand cmd)
    {
        using var ms = new MemoryStream(cmd.Data);
        using var reader = new BinaryReader(ms);

        // FIX: Photon fragments use Unsigned Ints for sequence limits
        uint sequenceNumber = reader.ReadBigEndianUInt32();
        uint fragmentCount = reader.ReadBigEndianUInt32();
        uint fragmentNumber = reader.ReadBigEndianUInt32();
        uint totalLength = reader.ReadBigEndianUInt32(); 
        uint fragmentOffset = reader.ReadBigEndianUInt32();
        
        byte[] fragmentData = reader.ReadBytes((int)(ms.Length - ms.Position));

        lock (_lockObj)
        {
            if (!_sequences.TryGetValue(sequenceNumber, out var seq))
            {
                seq = new FragmentSequence
                {
                    FragmentsReceived = 0,
                    TotalFragments = fragmentCount,
                    Buffer = new byte[totalLength] 
                };
                _sequences[sequenceNumber] = seq;
            }

            Buffer.BlockCopy(fragmentData, 0, seq.Buffer, (int)fragmentOffset, fragmentData.Length);
            seq.FragmentsReceived++;

            if (seq.FragmentsReceived == seq.TotalFragments)
            {
                _sequences.Remove(sequenceNumber);
                return new PhotonCommand
                {
                    Type = CommandType.SendReliableType,
                    Data = seq.Buffer
                };
            }
        }

        return null;
    }
}