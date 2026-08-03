using System;
using AlbionBot.Core.Enums;
using AlbionBot.Network;
using AlbionBot.Network.Buffers;
using AlbionBot.Network.Parsers;

namespace AlbionBot.Host;

class Program
{
    private static readonly FragmentBuffer _fragBuffer = new();

    static void Main(string[] args)
    {
        Console.WriteLine("Starting AlbionBot Native Host...");

        // Ensure you are referencing your Sniffer.cs from earlier!
        using var sniffer = new Sniffer();
        
        // Subscribe to raw UDP payloads
        sniffer.OnPhotonPacketReceived += Sniffer_OnPhotonPacketReceived;

        // Start Sniffer (can pass "Killer", "Intel", etc. to auto-target adapter)
        sniffer.Start();

        Console.WriteLine("Press ENTER to close the application...");
        Console.ReadLine();
        
        sniffer.Stop();
    }

    private static void Sniffer_OnPhotonPacketReceived(object? sender, byte[] payload)
    {
        try 
        {
            var layer = PhotonLayer.Unpack(payload);

            foreach (var cmd in layer.Commands)
            {
                if (cmd.Type == CommandType.SendReliableType)
                {
                    ProcessReliableCommand(cmd.Data);
                }
                else if (cmd.Type == CommandType.SendReliableFragmentType)
                {
                    var reassembledCmd = _fragBuffer.Offer(cmd);
                    if (reassembledCmd != null)
                    {
                        ProcessReliableCommand(reassembledCmd.Data);
                    }
                }
            }
        }
        catch
        {
            // Ignore corrupted/malformed packets safely
        }
    }

    private static void ProcessReliableCommand(byte[] reliableData)
    {
        // Coming up next: Gzip decompression and Photon Protocol 16 mapping!
        Console.WriteLine($"[Process] Reliable Command ready! Size: {reliableData.Length} bytes.");
    }
}