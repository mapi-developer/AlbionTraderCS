using System.Collections.Generic;

namespace AlbionBot.Core.Models;

public enum MessageType : byte
{
    Request = 2,
    Response = 3,
    Event = 4,
    OtherResponse = 7 // Albion heavily uses this for Market Data
}

public class PhotonMessage
{
    public MessageType MessageType { get; set; }
    public byte Code { get; set; }
    public short ReturnCode { get; set; }
    public string? DebugMessage { get; set; }
    public Dictionary<byte, object> Parameters { get; set; } = new();
}