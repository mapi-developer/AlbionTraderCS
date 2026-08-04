using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Text;
using AlbionBot.Infrastructure;

namespace AlbionBot.Protocol;

public class Protocol16Reader
{
    private readonly BinaryReader _reader;

    public Protocol16Reader(ReadOnlyMemory<byte> data)
    {
        _reader = new BinaryReader(new MemoryStream(data.ToArray()));
    }

    public bool TryReadEventOrResponse(out IDictionary<byte, object?> result)
    {
        result = new Dictionary<byte, object?>();

        if (_reader.BaseStream.Length - _reader.BaseStream.Position < 2) return false;

        var signature = _reader.ReadByte();
        if (signature != 0xF3)
        {
            // Silently ignore ping/unreliable packets that aren't valid Protocol16 messages
            return false; 
        }

        var messageType = _reader.ReadByte();
        var code = _reader.ReadByte(); // Operation Code or Event Code

        if (messageType == 3 || messageType == 7) // Standard Response or Custom Albion Response
        {
            var returnCode = ReadInt16Value();
            var debugMsgType = _reader.ReadByte();
            
            if (debugMsgType == 0x73 || debugMsgType == 8) // String
            {
                ReadString(); // Skip debug message string
            }
        }

        // Now read the parameter count
        if (_reader.BaseStream.Length - _reader.BaseStream.Position < 2) return false;
        
        var parameterCount = ReadInt16Value();
        
        for (var i = 0; i < parameterCount; i++)
        {
            var key = _reader.ReadByte();
            result[key] = ReadValue();
        }

        return true;
    }

    public object? ReadValue()
    {
        var marker = _reader.ReadByte();
        return marker switch
        {
            0x2A => null, // Null
            0x44 => ReadDictionary(), // 'D' Dictionary
            0x61 => ReadArray(), // 'a' String array
            0x62 => ReadByteValue(), // 'b' Byte
            0x63 => ReadShortString(), // 'c' Short string
            0x64 => ReadUInt16Value(), // 'd'
            0x65 => ReadUInt32Value(), // 'e'
            0x66 => ReadSingleValue(), // 'f' Float
            0x69 => ReadInt32Value(), // 'i' Int32
            0x6B => ReadInt16Value(), // 'k' Int16
            0x6C => ReadInt64Value(), // 'l' Int64
            0x73 => ReadString(), // 's' String
            0x78 => ReadByteArray(), // 'x' Byte array
            0x79 => ReadObjectArray(), // 'y' Object array
            // Albion Custom Compressed Types
            1 => ReadByteValue() != 0, // Boolean
            2 => ReadByteValue(),      // Byte
            3 => ReadInt16Value(),     // Short
            4 => ReadInt32Value(),     // Int
            5 => ReadInt64Value(),     // Long
            6 => ReadSingleValue(),    // Float
            7 => ReadDoubleValue(),    // Double
            8 => ReadString(),         // String
            _ => throw new InvalidDataException($"Unsupported Photon type marker 0x{marker:X2}")
        };
    }

    private IDictionary<object, object?> ReadDictionary()
    {
        _reader.ReadByte(); // Key type
        _reader.ReadByte(); // Value type
        var length = ReadInt16Value();
        var dict = new Dictionary<object, object?>();
        for (var i = 0; i < length; i++)
        {
            var key = ReadValue();
            var val = ReadValue();
            if (key != null) dict[key] = val;
        }
        return dict;
    }

    private object?[] ReadArray()
    {
        var length = ReadInt16Value();
        var items = new object?[length];
        for (var i = 0; i < length; i++) items[i] = ReadValue();
        return items;
    }

    private byte ReadByteValue() => _reader.ReadByte();
    private ushort ReadUInt16Value() => BinaryPrimitives.ReadUInt16BigEndian(_reader.ReadBytes(2));
    private uint ReadUInt32Value() => BinaryPrimitives.ReadUInt32BigEndian(_reader.ReadBytes(4));
    private float ReadSingleValue() => BinaryPrimitives.ReadSingleBigEndian(_reader.ReadBytes(4));
    private double ReadDoubleValue() => BinaryPrimitives.ReadDoubleBigEndian(_reader.ReadBytes(8));
    private int ReadInt32Value() => BinaryPrimitives.ReadInt32BigEndian(_reader.ReadBytes(4));
    private short ReadInt16Value() => BinaryPrimitives.ReadInt16BigEndian(_reader.ReadBytes(2));
    private long ReadInt64Value() => BinaryPrimitives.ReadInt64BigEndian(_reader.ReadBytes(8));

    private string ReadShortString()
    {
        var length = _reader.ReadByte();
        return Encoding.UTF8.GetString(_reader.ReadBytes(length));
    }

    private string ReadString()
    {
        var length = ReadInt16Value();
        return Encoding.UTF8.GetString(_reader.ReadBytes(length));
    }

    private byte[] ReadByteArray()
    {
        var length = ReadInt32Value();
        return _reader.ReadBytes(length);
    }

    private object?[] ReadObjectArray()
    {
        var length = ReadInt16Value();
        var items = new object?[length];
        for (var i = 0; i < length; i++) items[i] = ReadValue();
        return items;
    }
}