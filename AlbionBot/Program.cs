using System;
using AlbionBot.Network;
using AlbionBot.Photon;

namespace AlbionBot;

class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("Starting AlbionBot Native Host...");

        // 1. Initialize our instances
        var parser = new AlbionParser();
        var sniffer = new Sniffer();

        // 2. Route the raw UDP bytes directly into 0blu's PhotonPackageParser
        sniffer.OnUdpPayloadCaptured += parser.ReceivePacket;

        // 3. Start sniffing
        sniffer.Start();

        Console.WriteLine("Waiting for Market Data...");
        Console.WriteLine("-> IMPORTANT: Open the market and hit 'Search' on an item!");
        
        Console.ReadLine();
        sniffer.Stop();
    }
}