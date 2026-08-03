using System;
using System.Collections.Generic;
using System.IO;
using AlbionBot.Core.Models;

namespace AlbionBot.Network.Parsers;

public static class Protocol16Deserializer
{
    public static PhotonMessage? Deserialize(byte[] payload)
    {
        if (payload.Length < 2) return null;

        using var ms = new MemoryStream(payload);
        using var reader = new BinaryReader(ms);

        var msg = new PhotonMessage();

        byte signature = reader.ReadByte(); 
        msg.MessageType = (MessageType)reader.ReadByte();

        switch (msg.MessageType)
        {
            case MessageType.Request:
                msg.Code = reader.ReadByte();
                break;
            case MessageType.Response:
            case MessageType.OtherResponse: // Handle Albion's Custom Response Type
                msg.Code = reader.ReadByte();
                msg.ReturnCode = (short)reader.ReadBigEndianUInt16();
                byte debugType = reader.ReadByte();
                try {
                    msg.DebugMessage = ReadValue(reader, debugType)?.ToString();
                } catch { } // Ignore debug parse fails so we can keep reading Parameters
                break;
            case MessageType.Event:
                msg.Code = reader.ReadByte();
                break;
            default:
                return null;
        }

        if (reader.BaseStream.Position + 2 > reader.BaseStream.Length) return msg;
        
        ushort paramCount = reader.ReadBigEndianUInt16();
        for (int i = 0; i < paramCount; i++)
        {
            if (reader.BaseStream.Position >= reader.BaseStream.Length) break;
            
            try 
            {
                byte paramId = reader.ReadByte();
                byte paramType = reader.ReadByte();
                
                var value = ReadValue(reader, paramType);
                if (value != null)
                {
                    msg.Parameters[paramId] = value;
                }
            }
            catch
            {
                // FAIL-SAFE: Break the loop but save the parameters successfully parsed so far!
                break; 
            }
        }

        return msg;
    }

    private static object? ReadValue(BinaryReader reader, byte typeCode)
    {
        return typeCode switch
        {
            0 or 42 => null, // Nil
            98 or 3 => reader.ReadByte(), // Byte
            107 or 7 => reader.ReadBigEndianInt16(), // Short ('k' -> 7)
            105 or 5 => reader.ReadBigEndianInt32(), // Int32 ('i' -> 5)
            108 or 8 => reader.ReadBigEndianInt64(), // Int64 ('l' -> 8)
            102 or 2 => reader.ReadBigEndianSingle(), // Float32 ('f' -> 2)
            100 => reader.ReadBigEndianDouble(), // Double ('d')
            115 => reader.ReadPhotonString(), // String ('s')
            111 or 1 => reader.ReadByte() != 0, // Boolean ('o' -> 1)
            120 => reader.ReadBytes((int)reader.ReadBigEndianUInt32()), // Byte Array ('x')
            121 => ReadArray(reader), // Array (Slice) ('y')
            97 => ReadStringArray(reader), // String Array ('a')
            122 => ReadObjectArray(reader), // Object Array ('z')
            68 => ReadDictionary(reader), // Dictionary ('D')
            104 => ReadHashtable(reader), // Hashtable ('h')
            _ => throw new Exception($"Unsupported Photon Type: {typeCode}")
        };
    }

    private static object[] ReadArray(BinaryReader reader)
    {
        ushort size = reader.ReadBigEndianUInt16();
        byte elementType = reader.ReadByte();
        var arr = new object[size];
        for (int i = 0; i < size; i++) arr[i] = ReadValue(reader, elementType)!;
        return arr;
    }

    private static string[] ReadStringArray(BinaryReader reader)
    {
        ushort size = reader.ReadBigEndianUInt16();
        var arr = new string[size];
        for (int i = 0; i < size; i++) arr[i] = reader.ReadPhotonString();
        return arr;
    }

    private static object?[] ReadObjectArray(BinaryReader reader)
    {
        ushort size = reader.ReadBigEndianUInt16();
        var arr = new object?[size];
        for (int i = 0; i < size; i++)
        {
            byte elementType = reader.ReadByte();
            arr[i] = ReadValue(reader, elementType);
        }
        return arr;
    }

    private static Dictionary<object, object> ReadDictionary(BinaryReader reader)
    {
        byte keyType = reader.ReadByte();
        byte valueType = reader.ReadByte();
        ushort size = reader.ReadBigEndianUInt16();
        var dict = new Dictionary<object, object>();
        for (int i = 0; i < size; i++)
        {
            var key = ReadValue(reader, keyType);
            var val = ReadValue(reader, valueType);
            if (key != null && val != null) dict[key] = val;
        }
        return dict;
    }

    private static Dictionary<object, object> ReadHashtable(BinaryReader reader)
    {
        ushort size = reader.ReadBigEndianUInt16();
        var dict = new Dictionary<object, object>();
        for (int i = 0; i < size; i++)
        {
            byte keyType = reader.ReadByte();
            var key = ReadValue(reader, keyType);
            byte valueType = reader.ReadByte();
            var val = ReadValue(reader, valueType);
            if (key != null && val != null) dict[key] = val;
        }
        return dict;
    }
}