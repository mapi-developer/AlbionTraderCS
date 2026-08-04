using System.Text.Json;
using System.Text.Json.Serialization;
using AlbionBot.Infrastructure;
using AlbionBot.Models;

namespace AlbionBot.Services;

public sealed class JsonEventBroadcaster
{
    private readonly JsonSerializerOptions _options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true
    };

    public string SerializeMarketOrder(MarketOrder order)
    {
        DebugLogger.Log($"Broadcasting market order id={order.Id}");
        return JsonSerializer.Serialize(new { type = "market_order", data = order }, _options);
    }

    public string SerializePosition(PlayerPosition position)
    {
        DebugLogger.Log($"Broadcasting position x={position.X} y={position.Y}");
        return JsonSerializer.Serialize(new { type = "player_position", data = position }, _options);
    }

    public string SerializeInventory(IEnumerable<InventoryItem> items)
    {
        DebugLogger.Log("Broadcasting inventory update.");
        return JsonSerializer.Serialize(new { type = "inventory", data = items }, _options);
    }

    public string SerializeSilver(SilverUpdate silver)
    {
        DebugLogger.Log($"Broadcasting silver update silver={silver.Silver} gold={silver.Gold}");
        return JsonSerializer.Serialize(new { type = "silver_update", data = silver }, _options);
    }
}
