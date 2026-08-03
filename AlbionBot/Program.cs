using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using System.Text.Json;
using AlbionBot.Network;
using AlbionBot.Photon;
using AlbionBot.Models;

namespace AlbionBot;

class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("Starting AlbionBot Native Host...");

        var parser = new AlbionParser();
        var sniffer = new Sniffer();

        // Fix: Subscribing to the correct sniffer event name
        sniffer.OnUdpPayloadCaptured += (payload) =>
        {
            parser.ReceiveUdpPayload(payload);
        };

        parser.OnMessageReady += (message) =>
        {
            // Code 1, 75, or 76 represent Market / Auction operations
            if (message.Code == 1 || message.Code == 75 || message.Code == 76)
            {
                ScrapeMarketData(message.RawPayload);
            }
        };

        sniffer.Start();

        Console.WriteLine("\nWaiting for Market Data...");
        Console.WriteLine("-> IMPORTANT: Open the market and search for an item!\n");
        
        Console.ReadLine();
        sniffer.Stop();
    }

    private static void ScrapeMarketData(byte[] rawPayload)
    {
        try
        {
            // Decompress if gzipped
            byte[] decompressed = DecompressIfNeeded(rawPayload);
            string rawText = Encoding.UTF8.GetString(decompressed);
            
            // Extract individual JSON market items using Regex
            var matches = Regex.Matches(rawText, @"\{""Id"":[^}]+\}");

            if (matches.Count == 0) return;

            Console.WriteLine($"\n[Market] Intercepted {matches.Count} Market Orders!");

            foreach (Match match in matches)
            {
                try
                {
                    var order = JsonSerializer.Deserialize<MarketOrder>(match.Value);
                    if (order != null)
                    {
                        // Albion tracks silver in 10000 increments
                        Console.WriteLine($"  -> Item ID: {order.ItemTypeId} | Qty: {order.Amount} | Price: {order.UnitPriceSilver / 10000:N0} silver");
                    }
                }
                catch { /* Ignore invalid fragments */ }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Scraper Error]: {ex.Message}");
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