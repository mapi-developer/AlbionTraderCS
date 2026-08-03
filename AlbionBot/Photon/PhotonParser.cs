using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using AlbionBot.Models;

namespace AlbionBot.Photon;

public class PhotonParser
{
    public event Action<PhotonMessage>? OnMessageReady;

    // Buffer to hold fragmented packets
    private readonly Dictionary<uint, byte[]> _fragmentBuffers = new();
    private readonly Dictionary<uint, int> _fragmentsReceived = new();
    private readonly object _lock = new();

    public void ReceiveUdpPayload(byte[] payload)
    {
        try
        {
            using var ms = new MemoryStream(payload);
            using var reader = new BinaryReader(ms);

            // 12-byte Photon Layer Header
            reader.ReadBytes(2); // PeerId
            reader.ReadByte();   // Flags
            byte commandCount = reader.ReadByte();
            reader.ReadBytes(8); // Timestamp & Challenge

            for (int i = 0; i < commandCount; i++)
            {
                if (ms.Position >= ms.Length) break;

                byte commandType = reader.ReadByte();
                reader.ReadBytes(3); // ChannelId, Flags, Reserved
                int commandLength = ReadBigEndianInt32(reader);
                reader.ReadBytes(4); // ReliableSequenceNumber

                int dataLength = commandLength - 12;
                if (dataLength <= 0 || ms.Position + dataLength > ms.Length) break;

                byte[] commandData = reader.ReadBytes(dataLength);

                if (commandType == 6) // SendReliable (Full Packet)
                {
                    ProcessReliableData(commandData);
                }
                else if (commandType == 8) // SendReliableFragment (Partial Packet)
                {
                    HandleFragment(commandData);
                }
            }
        }
        catch { /* Ignore corrupted packets */ }
    }

    private void HandleFragment(byte[] fragmentData)
    {
        using var ms = new MemoryStream(fragmentData);
        using var reader = new BinaryReader(ms);

        uint sequenceNumber = ReadBigEndianUInt32(reader);
        uint fragmentCount = ReadBigEndianUInt32(reader);
        reader.ReadBytes(4); // fragmentNumber
        uint totalLength = ReadBigEndianUInt32(reader);
        uint fragmentOffset = ReadBigEndianUInt32(reader);
        
        byte[] payload = reader.ReadBytes((int)(ms.Length - ms.Position));

        lock (_lock)
        {
            if (!_fragmentBuffers.ContainsKey(sequenceNumber))
            {
                _fragmentBuffers[sequenceNumber] = new byte[totalLength];
                _fragmentsReceived[sequenceNumber] = 0;
            }

            Buffer.BlockCopy(payload, 0, _fragmentBuffers[sequenceNumber], (int)fragmentOffset, payload.Length);
            _fragmentsReceived[sequenceNumber]++;

            if (_fragmentsReceived[sequenceNumber] >= fragmentCount)
            {
                byte[] reassembled = _fragmentBuffers[sequenceNumber];
                _fragmentBuffers.Remove(sequenceNumber);
                _fragmentsReceived.Remove(sequenceNumber);
                
                ProcessReliableData(reassembled);
            }
        }
    }

    private void ProcessReliableData(byte[] data)
    {
        if (data.Length >= 2 && data[0] == 0x1F && data[1] == 0x8B)
        {
            using var compressedStream = new MemoryStream(data);
            using var gzipStream = new GZipStream(compressedStream, CompressionMode.Decompress);
            using var resultStream = new MemoryStream();
            gzipStream.CopyTo(resultStream);
            data = resultStream.ToArray();
        }

        // --- DEBUG HEX DUMP INJECTION ---
        // 243 (0xF3) = Photon Signature | 3 = Response | 1 = Code 1 (Market Data)
        if (data.Length > 10 && data[0] == 243 && data[1] == 3 && data[2] == 1)
        {
            Console.WriteLine("\n[DEBUG] CAUGHT MASSIVE MARKET PACKET!");
            Console.WriteLine($"[DEBUG] Total Size: {data.Length} bytes.");
            Console.WriteLine("[DEBUG] First 60 bytes (HEX):");
            
            // Print the first 60 bytes so we can see Albion's exact header alignment
            string hex = BitConverter.ToString(data, 0, Math.Min(data.Length, 60));
            Console.WriteLine(hex + "\n");
        }
        // --------------------------------

        var message = Protocol16Deserializer.Deserialize(data);
        if (message != null)
        {
            OnMessageReady?.Invoke(message);
        }
    }

    private static int ReadBigEndianInt32(BinaryReader reader)
    {
        byte[] b = reader.ReadBytes(4);
        return (b[0] << 24) | (b[1] << 16) | (b[2] << 8) | b[3];
    }

    private static uint ReadBigEndianUInt32(BinaryReader reader)
    {
        byte[] b = reader.ReadBytes(4);
        return (uint)((b[0] << 24) | (b[1] << 16) | (b[2] << 8) | b[3]);
    }
}