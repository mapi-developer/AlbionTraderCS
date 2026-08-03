namespace AlbionBot.Core.Enums;

public enum CommandType : byte
{
    Ping = 1,
    LogOut = 2,
    Disconnect = 4,
    SendReliableType = 6,
    SendUnreliableType = 7,
    SendReliableFragmentType = 8
}