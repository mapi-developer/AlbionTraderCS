using System.Collections.Concurrent;
using AlbionBot.Infrastructure;
using AlbionBot.Models;

namespace AlbionBot.Services;

public sealed class GameStateStore
{
    private readonly ConcurrentDictionary<long, MarketOrder> _marketOrders = new();
    private readonly ConcurrentDictionary<byte, InventoryItem> _inventory = new();
    private PlayerPosition _position = new(0, 0);
    private SilverUpdate _silver = new(0, 0);

    public event Action<MarketOrder>? OnMarketOrderUpdated;
    public event Action<PlayerPosition>? OnPositionChanged;
    public event Action<IEnumerable<InventoryItem>>? OnInventoryChanged;
    public event Action<SilverUpdate>? OnSilverChanged;

    public void UpdateMarketOrder(MarketOrder order)
    {
        DebugLogger.Log($"Updating market order id={order.Id} item={order.ItemTypeId} price={order.UnitPrice} qty={order.Quantity}");
        _marketOrders.AddOrUpdate(order.Id, order, (_, _) => order);
        OnMarketOrderUpdated?.Invoke(order);
    }

    public void UpdatePosition(PlayerPosition position)
    {
        DebugLogger.Log($"Updating player position x={position.X} y={position.Y}");
        _position = position;
        OnPositionChanged?.Invoke(position);
    }

    public void UpdateInventory(IEnumerable<InventoryItem> items)
    {
        DebugLogger.Log("Updating inventory items.");
        _inventory.Clear();
        foreach (var item in items)
        {
            DebugLogger.Log($"Inventory item slot={item.Slot} id={item.ItemId} qty={item.Quantity} durability={item.Durability}");
            _inventory[item.Slot] = item;
        }
        OnInventoryChanged?.Invoke(_inventory.Values);
    }

    public void UpdateSilver(SilverUpdate silver)
    {
        DebugLogger.Log($"Updating silver gold={silver.Gold} silver={silver.Silver}");
        _silver = silver;
        OnSilverChanged?.Invoke(silver);
    }

    public IReadOnlyCollection<MarketOrder> GetMarketOrders() => _marketOrders.Values.ToArray();
    public IReadOnlyCollection<InventoryItem> GetInventory() => _inventory.Values.ToArray();
    public PlayerPosition GetPlayerPosition() => _position;
    public SilverUpdate GetSilverUpdate() => _silver;
}
