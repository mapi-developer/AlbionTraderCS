using System.Collections.Generic;
using System.IO;

namespace AlbionBackend.Network
{
    public class PhotonCommand
    {
        public CommandType Type { get; set; }
        public byte ChannelId { get; set; }
        public byte Flags { get; set; }
        public int Length { get; set; }
        public int ReliableSequenceNumber { get; set; }
        public byte[] Data { get; set; } // The actual payload
    }

    public class PhotonLayer
    {
        public short PeerId { get; set; }
        public byte Flags { get; set; }
        public byte CommandCount { get; set; }
        public int Timestamp { get; set; }
        public int Challenge { get; set; }
        public List<PhotonCommand> Commands { get; set; } = new List<PhotonCommand>();

        public static PhotonLayer Unpack(byte[] payload)
        {
            var layer = new PhotonLayer();
            using var ms = new MemoryStream(payload);
            using var reader = new BinaryReader(ms);

            // 1. Parse the 12-byte Photon Header
            layer.PeerId = BigEndianReader.ReadInt16(reader);
            layer.Flags = reader.ReadByte();
            layer.CommandCount = reader.ReadByte();
            layer.Timestamp = BigEndianReader.ReadInt32(reader);
            layer.Challenge = BigEndianReader.ReadInt32(reader);

            // 2. Extract Commands
            for (int i = 0; i < layer.CommandCount; i++)
            {
                if (reader.BaseStream.Position >= reader.BaseStream.Length) break;

                var cmd = new PhotonCommand
                {
                    Type = (CommandType)reader.ReadByte(),
                    ChannelId = reader.ReadByte(),
                    Flags = reader.ReadByte(),
                    Skip Byte (Reserved)
                    reader.ReadByte(), 
                    
                    Length = BigEndianReader.ReadInt32(reader),
                    ReliableSequenceNumber = BigEndianReader.ReadInt32(reader)
                };

                // The length includes the 12-byte command header, so the payload is (Length - 12)
                int dataLength = cmd.Length - 12;
                
                if (dataLength > 0 && reader.BaseStream.Position + dataLength <= reader.BaseStream.Length)
                {
                    cmd.Data = reader.ReadBytes(dataLength);
                }
                else
                {
                    cmd.Data = new byte[0];
                }

                layer.Commands.Add(cmd);
            }

            return layer;
        }
    }
}