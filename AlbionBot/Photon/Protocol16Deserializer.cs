using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using AlbionBot.Models;

namespace AlbionBot.Photon;

public static class Protocol16Deserializer
{
    public static PhotonMessage? Deserialize(byte[] payload)
    {
        if (payload.Length < 2) return null;

        using var ms = new MemoryStream(payload);
        using var reader = new BinaryReader(ms);

        // SAFEGUARD: Only parse unencrypted Protocol 16 packets
        if (reader.ReadByte() != 243) return null; 

        var msg = new PhotonMessage { MessageType = (MessageType)reader.ReadByte() };

        if (msg.MessageType == MessageType.Request || msg.MessageType == MessageType.Event)
        {
            msg.Code = reader.ReadByte();
        }
        else if (msg.MessageType == MessageType.Response)
        {
            msg.Code = reader.ReadByte();
            reader.ReadBytes(2); // Skip ReturnCode
            byte debugType = reader.ReadByte();
            try { ReadValue(reader, debugType); } catch { } // Skip Debug string
        }
        else return null;

        if (ms.Position + 2 > ms.Length) return msg;
        
        ushort paramCount = ReadBigEndianUInt16(reader);
        for (int i = 0; i < paramCount; i++)
        {
            if (ms.Position >= ms.Length) break;
            try 
            {
                byte paramId = reader.ReadByte();
                byte paramType = reader.ReadByte();
                var value = ReadValue(reader, paramType);
                if (value != null) msg.Parameters[paramId] = value;
            }
            catch { break; } // Truncate cleanly on error
        }

        return msg;
    }

    private static object? ReadValue(BinaryReader reader, byte typeCode)
    {
        return typeCode switch
        {
            0 or 42 => null,
            111 => reader.ReadByte() != 0,
            98 => reader.ReadByte(),
            107 => ReadBigEndianInt16(reader),
            105 => ReadBigEndianInt32(reader),
            108 => ReadBigEndianInt64(reader),
            102 => ReadBigEndianSingle(reader),
            100 => ReadBigEndianDouble(reader),
            115 => ReadPhotonString(reader),
            120 => reader.ReadBytes(ReadBigEndianInt32(reader)), // Byte Array
            121 => ReadArray(reader),
            122 => ReadObjectArray(reader),
            97 => ReadStringArray(reader),
            110 => ReadIntArray(reader),
            68 => ReadDictionary(reader),
            101 => ReadEventData(reader),
            104 => ReadHashtable(reader),
            112 => ReadOperationResponse(reader),
            113 => ReadOperationRequest(reader),
            _ => throw new Exception($"Unknown Type: {typeCode}")
        };
    }

    // --- Type Parsers ---
    private static string ReadPhotonString(BinaryReader r)
    {
        ushort len = ReadBigEndianUInt16(r);
        return len == 0 ? string.Empty : Encoding.UTF8.GetString(r.ReadBytes(len));
    }
    private static object[] ReadArray(BinaryReader r) { var arr = new object[ReadBigEndianUInt16(r)]; byte type = r.ReadByte(); for(int i=0; i<arr.Length; i++) arr[i] = ReadValue(r, type)!; return arr; }
    private static string[] ReadStringArray(BinaryReader r) { var arr = new string[ReadBigEndianUInt16(r)]; for(int i=0; i<arr.Length; i++) arr[i] = ReadPhotonString(r); return arr; }
    private static int[] ReadIntArray(BinaryReader r) { var arr = new int[ReadBigEndianUInt16(r)]; for(int i=0; i<arr.Length; i++) arr[i] = ReadBigEndianInt32(r); return arr; }
    private static object?[] ReadObjectArray(BinaryReader r) { var arr = new object?[ReadBigEndianUInt16(r)]; for(int i=0; i<arr.Length; i++) arr[i] = ReadValue(r, r.ReadByte()); return arr; }
    
    private static Dictionary<object, object> ReadDictionary(BinaryReader r) { byte kType = r.ReadByte(), vType = r.ReadByte(); var dict = new Dictionary<object, object>(); ushort size = ReadBigEndianUInt16(r); for(int i=0; i<size; i++) { var k = ReadValue(r, kType); var v = ReadValue(r, vType); if(k!=null && v!=null) dict[k] = v; } return dict; }
    private static Dictionary<object, object> ReadHashtable(BinaryReader r) { ushort size = ReadBigEndianUInt16(r); var dict = new Dictionary<object, object>(); for(int i=0; i<size; i++) { var k = ReadValue(r, r.ReadByte()); var v = ReadValue(r, r.ReadByte()); if(k!=null && v!=null) dict[k] = v; } return dict; }
    
    private static object ReadEventData(BinaryReader r) { r.ReadByte(); ushort count = ReadBigEndianUInt16(r); var p = new Dictionary<byte, object>(); for(int i=0; i<count; i++) { byte id = r.ReadByte(); var val = ReadValue(r, r.ReadByte()); if(val != null) p[id] = val; } return p; }
    private static object ReadOperationResponse(BinaryReader r) { r.ReadByte(); r.ReadBytes(2); try { ReadValue(r, r.ReadByte()); } catch {} ushort count = ReadBigEndianUInt16(r); var p = new Dictionary<byte, object>(); for(int i=0; i<count; i++) { byte id = r.ReadByte(); var val = ReadValue(r, r.ReadByte()); if(val != null) p[id] = val; } return p; }
    private static object ReadOperationRequest(BinaryReader r) { r.ReadByte(); ushort count = ReadBigEndianUInt16(r); var p = new Dictionary<byte, object>(); for(int i=0; i<count; i++) { byte id = r.ReadByte(); var val = ReadValue(r, r.ReadByte()); if(val != null) p[id] = val; } return p; }

    // --- Endian Helpers ---
    private static short ReadBigEndianInt16(BinaryReader r) { byte[] b = r.ReadBytes(2); return (short)((b[0] << 8) | b[1]); }
    private static ushort ReadBigEndianUInt16(BinaryReader r) { byte[] b = r.ReadBytes(2); return (ushort)((b[0] << 8) | b[1]); }
    private static int ReadBigEndianInt32(BinaryReader r) { byte[] b = r.ReadBytes(4); return (b[0] << 24) | (b[1] << 16) | (b[2] << 8) | b[3]; }
    private static long ReadBigEndianInt64(BinaryReader r) { byte[] b = r.ReadBytes(8); if (BitConverter.IsLittleEndian) Array.Reverse(b); return BitConverter.ToInt64(b, 0); }
    private static float ReadBigEndianSingle(BinaryReader r) { byte[] b = r.ReadBytes(4); if (BitConverter.IsLittleEndian) Array.Reverse(b); return BitConverter.ToSingle(b, 0); }
    private static double ReadBigEndianDouble(BinaryReader r) { byte[] b = r.ReadBytes(8); if (BitConverter.IsLittleEndian) Array.Reverse(b); return BitConverter.ToDouble(b, 0); }
}