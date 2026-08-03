using System.IO;

namespace AlbionBot.Network.Parsers;

public static class BinaryReaderExtensions
{
    public static short ReadBigEndianInt16(this BinaryReader reader)
    {
        byte[] bytes = reader.ReadBytes(2);
        return (short)((bytes[0] << 8) | bytes[1]);
    }

    public static int ReadBigEndianInt32(this BinaryReader reader)
    {
        byte[] bytes = reader.ReadBytes(4);
        return (bytes[0] << 24) | (bytes[1] << 16) | (bytes[2] << 8) | bytes[3];
    }
}