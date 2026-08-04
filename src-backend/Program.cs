using System;
using System.IO.Compression;
using System.Text; // Make sure this is added
using System.Threading;
using System.Threading.Tasks;
using AlbionBot.Albion;
using AlbionBot.Infrastructure;
using AlbionBot.Models;
using AlbionBot.Network;
using AlbionBot.Protocol;
using AlbionBot.Services;

// 1. Turn off spammy logs so you ONLY see market data!
DebugLogger.Enabled = false; 
Console.WriteLine("Starting Albion Online packet sniffer...");

var queue = new PacketBufferQueue();
var sniffer = new UdpSniffer(queue);
var decoder = new PhotonProtocolDecoder();
var stateStore = new GameStateStore();
var broadcaster = new JsonEventBroadcaster();

try
{
    sniffer.Start();
}
catch (Exception ex)
{
    Console.WriteLine($"Failed to start packet sniffer: {ex.Message}");
    return;
}

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, args) =>
{
    args.Cancel = true;
    cts.Cancel();
};

await foreach (var buffer in queue.ReadAllAsync(cts.Token))
{
    try
    {
        foreach (var packet in decoder.DecodeRawPayload(buffer))
        {
            if (packet.CommandType != 0x06 && packet.CommandType != 0x07)
            {
                continue;
            }

            try
            {
                byte[] payloadData = packet.Payload.ToArray();

                // Handle optional GZIP decompression
                if (payloadData.Length >= 2 && payloadData[0] == 0x1F && payloadData[1] == 0x8B)
                {
                    using var compressedStream = new MemoryStream(payloadData);
                    using var gzipStream = new GZipStream(compressedStream, CompressionMode.Decompress);
                    using var resultStream = new MemoryStream();
                    gzipStream.CopyTo(resultStream);
                    payloadData = resultStream.ToArray();
                }

                // 2. INTERCEPT RAW JSON: Search the payload for Albion Market JSON
                string rawText = Encoding.UTF8.GetString(payloadData);
                if (rawText.Contains("\"UnitPriceSilver\"") && rawText.Contains("\"ItemTypeId\""))
                {
                    // Find where the JSON starts and ends
                    int startIndex = rawText.IndexOf("{\"Id\":");
                    if (startIndex == -1) startIndex = rawText.IndexOf("[{\"Id\":");

                    if (startIndex != -1)
                    {
                        int endIndex = rawText.LastIndexOf('}');
                        if (endIndex != -1 && endIndex > startIndex)
                        {
                            // Include the closing array bracket if it exists
                            if (rawText.Length > endIndex + 1 && rawText[endIndex + 1] == ']')
                            {
                                endIndex++; 
                            }
                            
                            string marketJson = rawText.Substring(startIndex, endIndex - startIndex + 1);
                            
                            Console.ForegroundColor = ConsoleColor.Green;
                            Console.WriteLine("\n=== [LIVE MARKET DATA FOUND] ===");
                            Console.WriteLine(marketJson);
                            Console.WriteLine("================================\n");
                            Console.ResetColor();
                        }
                    }
                    
                    // Skip passing this massive string to the Photon parser so it doesn't crash!
                    continue; 
                }

                // 3. Normal processing for small/standard packets
                var reader = new Protocol16Reader(payloadData);
                if (!reader.TryReadEventOrResponse(out var parsedParameters))
                {
                    continue;
                }

                var silverUpdate = AlbionEventDecoder.DecodeSilverUpdate(parsedParameters);
                if (silverUpdate != null) stateStore.UpdateSilver(silverUpdate);

                var position = AlbionEventDecoder.DecodePlayerPosition(parsedParameters);
                if (position != null) stateStore.UpdatePosition(position);

                var inventoryItems = AlbionEventDecoder.DecodeInventoryItems(parsedParameters);
                stateStore.UpdateInventory(inventoryItems);
            }
            catch (Exception)
            {
                // Silently ignore remaining parser misalignments
            }
        }
    }
    catch (Exception)
    {
        // Silently ignore packet queue errors
    }
}

sniffer.Stop();
Console.WriteLine("Stopped.");