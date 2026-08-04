using System;
using System.Buffers.Binary;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace AlbionBot.Protocol;

public class Protocol16Reader
{
    private readonly BinaryReader _reader;

    public Protocol16Reader(ReadOnlyMemory<byte> data)
    {
        _reader = new BinaryReader(new MemoryStream(data.ToArray()));
    }

    public IDictionary<byte, object?> ReadParameterDictionary()
    {
        var marker = _reader.ReadByte();
        if (marker != 0x44) // 'D'
        {
            throw new InvalidDataException($"Expected dictionary marker 0x44, got 0x{marker:X2}");
        }

        var dictionary = new Dictionary<byte, object?>();
        var parameterCount = _reader.ReadUInt16();

        for (var i = 0; i < parameterCount; i++)
        {
            var key = _reader.ReadByte();
            var value = ReadValue();
            dictionary[key] = value;
        }

        return dictionary;
    }

    public object? ReadValue()
    {
        var marker = _reader.ReadByte();
        return marker switch
        {
            0x61 => ReadArray(),
            0x62 => _reader.ReadByte(),
            0x63 => ReadShortString(),
            0x64 => _reader.ReadUInt16(),
            0x65 => _reader.ReadUInt32(),
            0x66 => _reader.ReadSingle(),
            0x69 => _reader.ReadInt32(),
            0x6B => _reader.ReadInt16(),
            0x6C => _reader.ReadInt64(),
            0x73 => ReadString(),
            0x78 => ReadByteArray(),
            0x79 => ReadObjectArray(),
            0x4E => null, // 'N' Photon null
            _ => throw new InvalidDataException($"Unsupported Photon type marker 0x{marker:X2}")
        };
    }

    private object?[] ReadArray()
    {
        var length = _reader.ReadInt32();
        var items = new object?[length];
        for (var i = 0; i < length; i++)
        {
            items[i] = ReadValue();
        }
        return items;
    }

    private string ReadShortString()
    {
        var length = _reader.ReadByte();
        var bytes = _reader.ReadBytes(length);
        return Encoding.UTF8.GetString(bytes);
    }

    private string ReadString()
    {
        var length = _reader.ReadUInt16();
        var bytes = _reader.ReadBytes(length);
        return Encoding.UTF8.GetString(bytes);
    }

    private byte[] ReadByteArray()
    {
        var length = _reader.ReadInt32();
        return _reader.ReadBytes(length);
    }

    private object?[] ReadObjectArray()
    {
        var length = _reader.ReadInt32();
        var items = new object?[length];
        for (var i = 0; i < length; i++)
        {
            items[i] = ReadValue();
        }
        return items;
    }
}
