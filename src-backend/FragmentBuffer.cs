using System;
using System.Collections.Generic;
using System.IO;

namespace AlbionBackend.Network
{
    public class FragmentBuffer
    {
        // Maps SequenceNumber to a dictionary of FragmentID -> Payload bytes
        private Dictionary<int, Dictionary<int, byte[]>> _buffers = new();
        // Maps SequenceNumber to the total number of fragments expected
        private Dictionary<int, int> _fragmentCounts = new();

        public PhotonCommand? Offer(PhotonCommand cmd)
        {
            using var ms = new MemoryStream(cmd.Data);
            using var reader = new BinaryReader(ms);

            // Read Fragment Header (12 bytes)
            int sequenceNumber = BigEndianReader.ReadInt32(reader);
            int fragmentCount = BigEndianReader.ReadInt32(reader);
            int fragmentNumber = BigEndianReader.ReadInt32(reader);
            int totalLength = BigEndianReader.ReadInt32(reader); // Length of reassembled payload
            int fragmentOffset = BigEndianReader.ReadInt32(reader);
            
            byte[] fragmentData = reader.ReadBytes((int)(ms.Length - ms.Position));

            // Initialize buffer for this sequence if it doesn't exist
            if (!_buffers.ContainsKey(sequenceNumber))
            {
                _buffers[sequenceNumber] = new Dictionary<int, byte[]>();
                _fragmentCounts[sequenceNumber] = fragmentCount;
            }

            // Store the fragment
            _buffers[sequenceNumber][fragmentNumber] = fragmentData;

            // Check if we have all fragments
            if (_buffers[sequenceNumber].Count == fragmentCount)
            {
                return Reassemble(sequenceNumber);
            }

            return null; // Still waiting for more fragments
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

            // Return a mocked "Reliable" command containing the stitched payload
            return new PhotonCommand
            {
                Type = CommandType.SendReliableType,
                Data = ms.ToArray()
            };
        }
    }
}