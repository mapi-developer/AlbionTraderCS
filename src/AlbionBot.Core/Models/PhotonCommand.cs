using AlbionBot.Core.Enums;

namespace AlbionBot.Core.Models;

public class PhotonCommand
{
    public CommandType Type { get; set; }
    public byte ChannelId { get; set; }
    public byte Flags { get; set; }
    public byte Reserved { get; set; }
    public int Length { get; set; }
    public int ReliableSequenceNumber { get; set; }
    
    // Initialized to empty to avoid nullable warnings
    public byte[] Data { get; set; } = []; 
}