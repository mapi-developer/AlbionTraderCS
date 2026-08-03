using System.IO;

namespace AlbionBackend.Network
{
    public static class BigEndianReader
    {
        public static short ReadInt16(BinaryReader reader)
        {
            byte[] bytes = reader.ReadBytes(2);
            return (short)((bytes[0] << 8) | bytes[1]);
        }

        public static int ReadInt32(BinaryReader reader)
        {
            byte[] bytes = reader.ReadBytes(4);
            return (bytes[0] << 24) | (bytes[1] << 16) | (bytes[2] << 8) | bytes[3];
        }
    }
}