using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using AlbionBot.Albion;
using AlbionBot.Infrastructure;
using AlbionBot.Services;
using PhotonPackageParser;

namespace AlbionBot.Network;

public sealed class PhotonPacketParserAdapter : PhotonParser
{
    private readonly GameStateStore _stateStore;
    private readonly PhotonProtocolDecoder _protocolDecoder = new();

    public PhotonPacketParserAdapter(GameStateStore stateStore)
    {
        _stateStore = stateStore;
    }

    public void ProcessPacket(ReadOnlyMemory<byte> packetPayload)
    {
        if (packetPayload.IsEmpty)
        {
            return;
        }

        try
        {
            DebugLogger.Log($"Processing packet payload length={packetPayload.Length}");
            DebugLogger.Log($"Packet bytes: {BitConverter.ToString(packetPayload.ToArray())}");

            foreach (var photonPacket in _protocolDecoder.DecodeRawPayload(packetPayload))
            {
                DebugLogger.Log($"Decoded Photon command type=0x{photonPacket.CommandType:X2} payloadLength={photonPacket.Payload.Length}");
                if (photonPacket.Payload.IsEmpty)
                {
                    DebugLogger.Log("Skipping empty Photon payload.");
                    continue;
                }

                ReceivePacket(photonPacket.Payload.ToArray());
            }
        }
        catch (Exception ex)
        {
            DebugLogger.Log($"PhotonPackageParser failed to process packet: {ex.Message}");
            DebugLogger.Log(ex.StackTrace ?? string.Empty);
        }
    }

    protected override void OnEvent(byte code, Dictionary<byte, object> parameters)
    {
        var typedParameters = new Dictionary<byte, object?>();
        foreach (var kvp in parameters)
        {
            typedParameters[kvp.Key] = kvp.Value;
        }

        DebugLogger.Log($"Photon event code=0x{code:X2} parameterCount={parameters.Count}");
        DebugLogger.Log("Photon event raw parameters:");
        foreach (var kvp in parameters)
        {
            DebugLogger.Log($"  key=0x{kvp.Key:X2}, type={kvp.Value?.GetType().Name ?? "null"}, value={FormatValue(kvp.Value)}");
        }

        try
        {
            var marketOrder = AlbionEventDecoder.DecodeMarketOrder(typedParameters);
            if (marketOrder != null)
            {
                DebugLogger.Log($"Decoded MarketOrder: {marketOrder}");
                _stateStore.UpdateMarketOrder(marketOrder);
            }
            else
            {
                DebugLogger.Log("Market order decode returned null.");
            }

            var silverUpdate = AlbionEventDecoder.DecodeSilverUpdate(typedParameters);
            if (silverUpdate != null)
            {
                DebugLogger.Log($"Decoded SilverUpdate: {silverUpdate}");
            }

            var position = AlbionEventDecoder.DecodePlayerPosition(typedParameters);
            if (position != null)
            {
                DebugLogger.Log($"Decoded PlayerPosition: {position}");
            }

            var inventoryItems = AlbionEventDecoder.DecodeInventoryItems(typedParameters);
            var inventoryList = inventoryItems.ToArray();
            if (inventoryList.Length > 0)
            {
                DebugLogger.Log($"Decoded InventoryItems count={inventoryList.Length}");
                foreach (var item in inventoryList)
                {
                    DebugLogger.Log($"  InventoryItem slot={item.Slot} id={item.ItemId} qty={item.Quantity} durability={item.Durability}");
                }
            }
        }
        catch (Exception ex)
        {
            DebugLogger.Log($"Albion event decode failed: {ex.Message}");
            DebugLogger.Log(ex.StackTrace ?? string.Empty);
        }
    }

    private static string FormatValue(object? value)
    {
        return value switch
        {
            byte[] bytes => BitConverter.ToString(bytes),
            object?[] array => "[" + string.Join(", ", Array.ConvertAll(array, FormatValue)) + "]",
            null => "null",
            _ => value.ToString() ?? string.Empty,
        };
    }

    protected override void OnRequest(byte operationCode, Dictionary<byte, object> parameters)
    {
        DebugLogger.Log($"Photon request operationCode=0x{operationCode:X2} parameterCount={parameters.Count}");
        DebugLogger.Log("Photon request raw parameters:");
        foreach (var kvp in parameters)
        {
            DebugLogger.Log($"  key=0x{kvp.Key:X2}, type={kvp.Value?.GetType().Name ?? "null"}, value={FormatValue(kvp.Value)}");
        }
    }

    protected override void OnResponse(byte operationCode, short returnCode, string debugMessage, Dictionary<byte, object> parameters)
    {
        DebugLogger.Log($"Photon response operationCode=0x{operationCode:X2} returnCode={returnCode} debugMessage={debugMessage} parameterCount={parameters.Count}");
        DebugLogger.Log("Photon response raw parameters:");
        foreach (var kvp in parameters)
        {
            DebugLogger.Log($"  key=0x{kvp.Key:X2}, type={kvp.Value?.GetType().Name ?? "null"}, value={FormatValue(kvp.Value)}");
        }
    }
}
