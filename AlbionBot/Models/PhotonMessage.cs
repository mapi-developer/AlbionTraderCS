using System.Collections.Generic;

namespace AlbionBot.Models;

public enum MessageType : byte
{
    Request = 2,
    Response = 3,
    Event = 4
}

public class PhotonMessage
{
    public MessageType MessageType { get; set; }
    public byte Code { get; set; }
    public Dictionary<byte, object> Parameters { get; set; } = new();
}