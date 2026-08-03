using System;
using System.IO;
using System.Text;

namespace AlbionBot.Network.Parsers;

public static class BinaryReaderExtensions
{
    public static short ReadBigEndianInt16(this BinaryReader reader)
    {
        byte[] bytes = reader.ReadBytes(2);
        return (short)((bytes[0] << 8) | bytes[1]);
    }

    public static ushort ReadBigEndianUInt16(this BinaryReader reader)
    {
        byte[] bytes = reader.ReadBytes(2);
        return (ushort)((bytes[0] << 8) | bytes[1]);
    }

    public static int ReadBigEndianInt32(this BinaryReader reader)
    {
        byte[] bytes = reader.ReadBytes(4);
        return (bytes[0] << 24) | (bytes[1] << 16) | (bytes[2] << 8) | bytes[3];
    }

    public static uint ReadBigEndianUInt32(this BinaryReader reader)
    {
        byte[] bytes = reader.ReadBytes(4);
        return (uint)((bytes[0] << 24) | (bytes[1] << 16) | (bytes[2] << 8) | bytes[3]);
    }

    public static long ReadBigEndianInt64(this BinaryReader reader)
    {
        byte[] bytes = reader.ReadBytes(8);
        if (BitConverter.IsLittleEndian) Array.Reverse(bytes);
        return BitConverter.ToInt64(bytes, 0);
    }

    public static float ReadBigEndianSingle(this BinaryReader reader)
    {
        byte[] bytes = reader.ReadBytes(4);
        if (BitConverter.IsLittleEndian) Array.Reverse(bytes);
        return BitConverter.ToSingle(bytes, 0);
    }

    public static double ReadBigEndianDouble(this BinaryReader reader)
    {
        byte[] bytes = reader.ReadBytes(8);
        if (BitConverter.IsLittleEndian) Array.Reverse(bytes);
        return BitConverter.ToDouble(bytes, 0);
    }

    public static string ReadPhotonString(this BinaryReader reader)
    {
        // FIX: Using UInt16 allows strings up to 65,535 bytes (Crucial for Market JSON)
        ushort length = reader.ReadBigEndianUInt16();
        if (length == 0) return string.Empty;
        
        byte[] strBytes = reader.ReadBytes(length);
        return Encoding.UTF8.GetString(strBytes);
    }
}