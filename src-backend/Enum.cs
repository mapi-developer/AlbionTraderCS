namespace AlbionBackend.Network
{
    public enum CommandType : byte
    {
        LogOut = 2,
        SendReliableType = 6,
        SendUnreliableType = 7,
        SendReliableFragmentType = 8,
        Disconnect = 4,
        Ping = 1
    }
}