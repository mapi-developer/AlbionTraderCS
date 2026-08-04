using System.Text.Json;
using System.Text.Json.Serialization;
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
        return JsonSerializer.Serialize(new { type = "market_order", data = order }, _options);
    }

    public string SerializePosition(PlayerPosition position)
    {
        return JsonSerializer.Serialize(new { type = "player_position", data = position }, _options);
    }

    public string SerializeInventory(IEnumerable<InventoryItem> items)
    {
        return JsonSerializer.Serialize(new { type = "inventory", data = items }, _options);
    }

    public string SerializeSilver(SilverUpdate silver)
    {
        return JsonSerializer.Serialize(new { type = "silver_update", data = silver }, _options);
    }
}
