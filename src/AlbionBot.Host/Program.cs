using System;
using System.IO;
using System.IO.Compression;
using System.Text.Json;
using AlbionBot.Core.Models;
using AlbionBot.Network;
using AlbionBot.Network.Parsers;

namespace AlbionBot.Host;

class Program
{
    private const byte OP_AUCTION_GET_OFFERS = 75;
    private const byte OP_AUCTION_GET_REQUESTS = 76;
    private const byte OP_EVENT_UPDATE_SILVER = 81;

    static void Main(string[] args)
    {
        Console.WriteLine("Starting AlbionBot Native Host...");

        using var sniffer = new Sniffer();
        sniffer.OnReliableCommandReady += Sniffer_OnReliableCommandReady;
        
        sniffer.Start();

        Console.WriteLine("Waiting for Market Data...");
        Console.WriteLine("-> IMPORTANT: Make sure you actually SEARCH for an item in the market UI!");
        
        Console.ReadLine();
        sniffer.Stop();
    }

    private static void Sniffer_OnReliableCommandReady(object? sender, byte[] reliableData)
    {
        try
        {
            // Log that we actually got a reassembled packet
            // Console.WriteLine($"[DEBUG] Reassembled Command Size: {reliableData.Length} bytes.");

            byte[] decompressedData = DecompressIfNeeded(reliableData);

            var message = Protocol16Deserializer.Deserialize(decompressedData);
            if (message == null) 
            {
                // Console.WriteLine("[DEBUG] Deserializer returned null.");
                return;
            }

            // --- DEBUG: Print every single response/event Albion sends ---
            // Uncomment the line below if you want to see a massive stream of EVERYTHING you do in game
            // Console.WriteLine($"[Incoming] Type: {message.MessageType} | Code: {message.Code} | Params: {message.Parameters.Count}");

            bool isResponse = message.MessageType == MessageType.Response || message.MessageType == MessageType.OtherResponse;

            if (isResponse && message.Code == OP_AUCTION_GET_OFFERS)
            {
                Console.WriteLine("[DEBUG] Successfully intercepted Auction Offers!");
                HandleMarketOrders(message, "SELL_OFFERS");
            }
            else if (isResponse && message.Code == OP_AUCTION_GET_REQUESTS)
            {
                Console.WriteLine("[DEBUG] Successfully intercepted Auction Requests!");
                HandleMarketOrders(message, "BUY_REQUESTS");
            }
            else if (message.MessageType == MessageType.Event && message.Code == OP_EVENT_UPDATE_SILVER)
            {
                if (message.Parameters.TryGetValue((byte)1, out var silverAmount))
                {
                    long trueSilver = Convert.ToInt64(silverAmount) / 10000; 
                    Console.WriteLine($"[PLAYER DATA] Silver Updated: {trueSilver:N0}");
                }
            }
        }
        catch (Exception ex)
        {
            // Print the exact reason the parser crashed
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"[Parse Crash]: {ex.Message}");
            Console.ResetColor();
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

    private static void HandleMarketOrders(PhotonMessage message, string type)
    {
        if (message.Parameters.TryGetValue((byte)0, out var rawData) && rawData is object[] stringArray)
        {
            Console.WriteLine($"\n--- RECEIVED {stringArray.Length} {type} ---");

            foreach (var item in stringArray)
            {
                string jsonString = item?.ToString() ?? "";
                if (string.IsNullOrWhiteSpace(jsonString)) continue;

                try
                {
                    var order = JsonSerializer.Deserialize<MarketOrder>(jsonString);
                    if (order != null)
                    {
                        decimal actualPrice = order.UnitPriceSilver / 10000m;
                        Console.WriteLine($"[{order.ItemTypeId}] Qty: {order.Amount} | Price: {actualPrice:N0} | Quality: {order.QualityLevel}");
                    }
                }
                catch (JsonException ex)
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine($"[JSON Parse Warning]: {ex.Message}");
                    Console.ResetColor();
                }
            }
        }
        else
        {
            Console.WriteLine("[DEBUG] Market packet found, but Parameter Key 0 was missing or not an array.");
        }
    }
}