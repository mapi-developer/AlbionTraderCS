using System;
using System.Threading;
using System.Threading.Tasks;
using AlbionBot.Albion;
using AlbionBot.Models;
using AlbionBot.Network;
using AlbionBot.Protocol;
using AlbionBot.Services;

Console.WriteLine("Starting Albion Online packet sniffer...");

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
    try
    {
        foreach (var packet in decoder.DecodeRawPayload(buffer))
        {
            try
            {
                var reader = new Protocol16Reader(packet.Payload);
                var parameters = reader.ReadParameterDictionary();

                var marketOrder = AlbionEventDecoder.DecodeMarketOrder(parameters);
                if (marketOrder != null)
                {
                    stateStore.UpdateMarketOrder(marketOrder);
                }

                var silverUpdate = AlbionEventDecoder.DecodeSilverUpdate(parameters);
                if (silverUpdate != null)
                {
                    stateStore.UpdateSilver(silverUpdate);
                }

                var position = AlbionEventDecoder.DecodePlayerPosition(parameters);
                if (position != null)
                {
                    stateStore.UpdatePosition(position);
                }

                var inventoryItems = AlbionEventDecoder.DecodeInventoryItems(parameters);
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
