using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using System.Text.Json;
using AlbionBot.Network;
using AlbionBot.Models;
using AlbionBot.Photon;

namespace AlbionBot.Host;

class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("Starting AlbionBot Native Host...");

        var sniffer = new Sniffer();
        sniffer.OnPhotonPacketReceived += Sniffer_OnReliableCommandReady;

        sniffer.Start();

        Console.WriteLine("\nWaiting for Market Data...");
        Console.WriteLine("-> IMPORTANT: Open the market and search for an item!\n");
        
        Console.ReadLine();
        sniffer.Stop();
    }

    private static void Sniffer_OnReliableCommandReady(object? sender, byte[] payload)
    {
        byte[] decompressed = DecompressIfNeeded(payload);
        
        var message = Protocol16Deserializer.Deserialize(decompressed);
        if (message != null)
        {
            // Code 1, 75, or 76 are common Market/Auction operations
            if (message.Code == 1 || message.Code == 75 || message.Code == 76)
            {
                ScrapeMarketData(message.RawPayload);
            }
        }
    }

    private static void ScrapeMarketData(byte[] rawPayload)
    {
        try
        {
            // 1. Convert the raw packet bytes directly to text
            string rawText = Encoding.UTF8.GetString(rawPayload);
            
            // 2. Use Regex to rip every flat JSON object out of the garbage bytes
            // This matches exactly: {"Id": [anything] }
            var matches = Regex.Matches(rawText, @"\{""Id"":[^}]+\}");

            if (matches.Count == 0) return;

            Console.WriteLine($"\n[Market] Intercepted {matches.Count} Market Orders!");

            // 3. Parse them cleanly into your C# object
            foreach (Match match in matches)
            {
                try
                {
                    var order = JsonSerializer.Deserialize<MarketOrder>(match.Value);
                    if (order != null)
                    {
                        // Print the clean data! (Divide silver by 10000 per Albion's math)
                        Console.WriteLine($"  -> {order.Amount}x (T{order.Tier}) @ {order.UnitPriceSilver / 10000:N0} silver");
                    }
                }
                catch 
                {
                    // Silently ignore corrupted fragments
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Scraper Error] {ex.Message}");
        }
    }

    private static byte[] DecompressIfNeeded(byte[] data)
    {
        if (data.Length >= 2 && data[0] == 0x1F && data[1] == 0x8B)
        {
            using var compressedStream = new MemoryStream(data);
            using var gzipStream = new GZipStream(compressedStream, CompressionMode.Decompress);
            using var resultStream = new MemoryStream();
            gzipStream.CopyTo(resultStream);
            return resultStream.ToArray();
        }
        return data;
    }
}