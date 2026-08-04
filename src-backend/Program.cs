using System;
using System.IO.Compression;
using System.Threading;
using System.Threading.Tasks;
using AlbionBot.Albion;
using AlbionBot.Infrastructure;
using AlbionBot.Models;
using AlbionBot.Network;
using AlbionBot.Protocol;
using AlbionBot.Services;

Console.WriteLine("Starting Albion Online packet sniffer...");
DebugLogger.Log("Starting Albion Online packet sniffer...");

var queue = new PacketBufferQueue();
var sniffer = new UdpSniffer(queue);
var decoder = new PhotonProtocolDecoder();
var stateStore = new GameStateStore();
var broadcaster = new JsonEventBroadcaster();

stateStore.OnMarketOrderUpdated += order => Console.WriteLine(broadcaster.SerializeMarketOrder(order));
stateStore.OnPositionChanged += position => Console.WriteLine(broadcaster.SerializePosition(position));
stateStore.OnInventoryChanged += items => Console.WriteLine(broadcaster.SerializeInventory(items));
stateStore.OnSilverChanged += silver => Console.WriteLine(broadcaster.SerializeSilver(silver));

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
    DebugLogger.Log($"Dequeued buffer length={buffer.Length}");
    try
    {
        foreach (var packet in decoder.DecodeRawPayload(buffer))
        {
            DebugLogger.Log($"Photon packet commandType=0x{packet.CommandType:X2} payloadLength={packet.Payload.Length}");
            if (packet.CommandType != 0x06 && packet.CommandType != 0x07)
            {
                DebugLogger.Log($"Skipping non-event packet type=0x{packet.CommandType:X2}");
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

                var reader = new Protocol16Reader(payloadData);

                // Use the new signature-checking event reader
                if (!reader.TryReadEventOrResponse(out var parsedParameters))
                {
                    continue;
                }

                DebugLogger.Log($"Parsed Photon dictionary with {parsedParameters.Count} keys.");

                var marketOrder = AlbionEventDecoder.DecodeMarketOrder(parsedParameters);
                // ... (rest of your decoding logic remains the same)
                if (marketOrder != null)
                {
                    stateStore.UpdateMarketOrder(marketOrder);
                }

                var silverUpdate = AlbionEventDecoder.DecodeSilverUpdate(parsedParameters);
                if (silverUpdate != null)
                {
                    stateStore.UpdateSilver(silverUpdate);
                }

                var position = AlbionEventDecoder.DecodePlayerPosition(parsedParameters);
                if (position != null)
                {
                    stateStore.UpdatePosition(position);
                }

                var inventoryItems = AlbionEventDecoder.DecodeInventoryItems(parsedParameters);
                stateStore.UpdateInventory(inventoryItems);
            }
            catch (Exception parseEx)
            {
                Console.WriteLine($"Protocol decode error: {parseEx.Message}");
            }
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Packet processing error: {ex.Message}");
    }
}

sniffer.Stop();
Console.WriteLine("Stopped.");
