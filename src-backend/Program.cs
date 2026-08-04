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

var queue = new PacketBufferQueue();
var sniffer = new UdpSniffer(queue);
var stateStore = new GameStateStore();
var broadcaster = new JsonEventBroadcaster();
var decoder = new PhotonPacketParserAdapter(stateStore);

// Only emit market JSON strings to console; suppress raw debug traffic.
DebugLogger.Enabled = false;
stateStore.OnMarketOrderUpdated += order => Console.WriteLine(broadcaster.SerializeMarketOrder(order));

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
    DebugLogger.Log($"Raw UDP payload: {BitConverter.ToString(buffer.ToArray())}");
    try
    {
        decoder.ProcessPacket(buffer);
    }
    catch (Exception ex)
    {
        DebugLogger.Log($"Packet processing exception: {ex.Message}");
        DebugLogger.Log(ex.StackTrace ?? string.Empty);
    }
}

sniffer.Stop();
Console.WriteLine("Stopped.");
