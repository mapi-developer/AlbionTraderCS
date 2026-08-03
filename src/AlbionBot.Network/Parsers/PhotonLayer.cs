using System.Collections.Generic;
using System.IO;
using AlbionBot.Core.Enums;
using AlbionBot.Core.Models;

namespace AlbionBot.Network.Parsers;

public class PhotonLayer
{
    public short PeerId { get; set; }
    public byte Flags { get; set; }
    public byte CommandCount { get; set; }
    public int Timestamp { get; set; }
    public int Challenge { get; set; }
    public List<PhotonCommand> Commands { get; set; } = new();

    public static PhotonLayer Unpack(byte[] payload)
    {
        var layer = new PhotonLayer();
        using var ms = new MemoryStream(payload);
        using var reader = new BinaryReader(ms);

        // 1. Parse the 12-byte Photon Header
        layer.PeerId = reader.ReadBigEndianInt16();
        layer.Flags = reader.ReadByte();
        layer.CommandCount = reader.ReadByte();
        layer.Timestamp = reader.ReadBigEndianInt32();
        layer.Challenge = reader.ReadBigEndianInt32();

        // 2. Extract Commands
        for (int i = 0; i < layer.CommandCount; i++)
        {
            if (reader.BaseStream.Position >= reader.BaseStream.Length) break;

            var cmd = new PhotonCommand
            {
                Type = (CommandType)reader.ReadByte(),
                ChannelId = reader.ReadByte(),
                Flags = reader.ReadByte(),
                Reserved = reader.ReadByte(), // Fixed syntax error here
                Length = reader.ReadBigEndianInt32(),
                ReliableSequenceNumber = reader.ReadBigEndianInt32()
            };

            // The length includes the 12-byte command header, so the payload is (Length - 12)
            int dataLength = cmd.Length - 12;
            
            if (dataLength > 0 && reader.BaseStream.Position + dataLength <= reader.BaseStream.Length)
            {
                cmd.Data = reader.ReadBytes(dataLength);
            }

            layer.Commands.Add(cmd);
        }

        return layer;
    }
}