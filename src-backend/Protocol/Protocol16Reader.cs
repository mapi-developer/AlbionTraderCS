using System;
using System.Buffers.Binary;
using System.Collections;
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

    public bool TryReadParameterDictionary(out IDictionary<byte, object?> result)
    {
        DebugLogger.Log($"Attempting to read Protocol16 parameter dictionary from payload length={_reader.BaseStream.Length}");
        result = new Dictionary<byte, object?>();

        if (_reader.BaseStream.Length - _reader.BaseStream.Position < 1)
        {
            DebugLogger.Log("Protocol16 payload empty.");
            return false;
        }

        var marker = _reader.ReadByte();
        if (marker != 0x44) // 'D'
        {
            DebugLogger.Log($"Unexpected Protocol16 marker 0x{marker:X2}.");
            return false;
        }

        if (_reader.BaseStream.Length - _reader.BaseStream.Position < 2)
        {
            DebugLogger.Log("Protocol16 payload missing dictionary count.");
            return false;
        }

        var parameterCount = _reader.ReadUInt16();
        DebugLogger.Log($"Protocol16 parameter dictionary count={parameterCount}");
        for (var i = 0; i < parameterCount; i++)
        {
            if (_reader.BaseStream.Length - _reader.BaseStream.Position < 1)
            {
                DebugLogger.Log("Protocol16 parameter dictionary truncated.");
                return false;
            }

            var key = _reader.ReadByte();
            try
            {
                var value = ReadValue();
                DebugLogger.Log($"Read Protocol16 parameter key={key} valueType={value?.GetType().Name ?? "null"}");
                result[key] = value;
            }
            catch (InvalidDataException ex)
            {
                DebugLogger.Log($"Protocol16 read error for key={key}: {ex.Message}");
                return false;
            }
        }

        return true;
    }

    public object? ReadValue()
    {
        if (_reader.BaseStream.Length - _reader.BaseStream.Position < 1)
        {
            throw new InvalidDataException("Unexpected end of Photon payload while reading a type marker.");
        }

        var marker = _reader.ReadByte();
        DebugLogger.Log($"Reading Photon value marker=0x{marker:X2}");
        return marker switch
        {
            0x61 => ReadArray(),
            0x62 => ReadByteValue(),
            0x63 => ReadShortString(),
            0x64 => ReadUInt16Value(),
            0x65 => ReadUInt32Value(),
            0x66 => ReadSingleValue(),
            0x69 => ReadInt32Value(),
            0x6B => ReadInt16Value(),
            0x6C => ReadInt64Value(),
            0x73 => ReadString(),
            0x78 => ReadByteArray(),
            0x79 => ReadObjectArray(),
            0x4E => null, // 'N' Photon null
            _ => throw new InvalidDataException($"Unsupported Photon type marker 0x{marker:X2}")
        };
    }

    private object?[] ReadArray()
    {
        if (_reader.BaseStream.Length - _reader.BaseStream.Position < 4)
        {
            throw new InvalidDataException("Unexpected end of Photon payload while reading array length.");
        }

        var length = _reader.ReadInt32();
        if (length < 0)
        {
            throw new InvalidDataException("Photon array length is invalid.");
        }

        if (_reader.BaseStream.Length - _reader.BaseStream.Position < length)
        {
            throw new InvalidDataException("Photon array length exceeds remaining payload.");
        }

        var items = new object?[length];
        for (var i = 0; i < length; i++)
        {
            items[i] = ReadValue();
        }
        return items;
    }

    private byte ReadByteValue()
    {
        if (_reader.BaseStream.Length - _reader.BaseStream.Position < 1)
        {
            throw new InvalidDataException("Unexpected end of Photon payload while reading byte value.");
        }

        return _reader.ReadByte();
    }

    private ushort ReadUInt16Value()
    {
        if (_reader.BaseStream.Length - _reader.BaseStream.Position < 2)
        {
            throw new InvalidDataException("Unexpected end of Photon payload while reading UInt16 value.");
        }

        return _reader.ReadUInt16();
    }

    private uint ReadUInt32Value()
    {
        if (_reader.BaseStream.Length - _reader.BaseStream.Position < 4)
        {
            throw new InvalidDataException("Unexpected end of Photon payload while reading UInt32 value.");
        }

        return _reader.ReadUInt32();
    }

    private float ReadSingleValue()
    {
        if (_reader.BaseStream.Length - _reader.BaseStream.Position < 4)
        {
            throw new InvalidDataException("Unexpected end of Photon payload while reading Single value.");
        }

        return _reader.ReadSingle();
    }

    private int ReadInt32Value()
    {
        if (_reader.BaseStream.Length - _reader.BaseStream.Position < 4)
        {
            throw new InvalidDataException("Unexpected end of Photon payload while reading Int32 value.");
        }

        return _reader.ReadInt32();
    }

    private short ReadInt16Value()
    {
        if (_reader.BaseStream.Length - _reader.BaseStream.Position < 2)
        {
            throw new InvalidDataException("Unexpected end of Photon payload while reading Int16 value.");
        }

        return _reader.ReadInt16();
    }

    private long ReadInt64Value()
    {
        if (_reader.BaseStream.Length - _reader.BaseStream.Position < 8)
        {
            throw new InvalidDataException("Unexpected end of Photon payload while reading Int64 value.");
        }

        return _reader.ReadInt64();
    }

    private string ReadShortString()
    {
        if (_reader.BaseStream.Length - _reader.BaseStream.Position < 1)
        {
            throw new InvalidDataException("Unexpected end of Photon payload while reading short string length.");
        }

        var length = _reader.ReadByte();
        if (_reader.BaseStream.Length - _reader.BaseStream.Position < length)
        {
            throw new InvalidDataException("Short string length exceeds remaining Photon payload.");
        }

        var bytes = _reader.ReadBytes(length);
        return Encoding.UTF8.GetString(bytes);
    }

    private string ReadString()
    {
        if (_reader.BaseStream.Length - _reader.BaseStream.Position < 2)
        {
            throw new InvalidDataException("Unexpected end of Photon payload while reading string length.");
        }

        var length = _reader.ReadUInt16();
        if (_reader.BaseStream.Length - _reader.BaseStream.Position < length)
        {
            throw new InvalidDataException("String length exceeds remaining Photon payload.");
        }

        var bytes = _reader.ReadBytes(length);
        return Encoding.UTF8.GetString(bytes);
    }

    private byte[] ReadByteArray()
    {
        if (_reader.BaseStream.Length - _reader.BaseStream.Position < 4)
        {
            throw new InvalidDataException("Unexpected end of Photon payload while reading byte array length.");
        }

        var length = _reader.ReadInt32();
        if (length < 0 || _reader.BaseStream.Length - _reader.BaseStream.Position < length)
        {
            throw new InvalidDataException("Byte array length exceeds remaining Photon payload.");
        }

        return _reader.ReadBytes(length);
    }

    private object?[] ReadObjectArray()
    {
        if (_reader.BaseStream.Length - _reader.BaseStream.Position < 4)
        {
            throw new InvalidDataException("Unexpected end of Photon payload while reading object array length.");
        }

        var length = _reader.ReadInt32();
        if (length < 0)
        {
            throw new InvalidDataException("Photon object array length is invalid.");
        }

        var items = new object?[length];
        for (var i = 0; i < length; i++)
        {
            items[i] = ReadValue();
        }
        return items;
    }
}
