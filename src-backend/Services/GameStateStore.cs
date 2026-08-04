using System.Collections.Concurrent;
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
        _marketOrders.AddOrUpdate(order.Id, order, (_, _) => order);
        OnMarketOrderUpdated?.Invoke(order);
    }

    public void UpdatePosition(PlayerPosition position)
    {
        _position = position;
        OnPositionChanged?.Invoke(position);
    }

    public void UpdateInventory(IEnumerable<InventoryItem> items)
    {
        _inventory.Clear();
        foreach (var item in items)
        {
            _inventory[item.Slot] = item;
        }
        OnInventoryChanged?.Invoke(_inventory.Values);
    }

    public void UpdateSilver(SilverUpdate silver)
    {
        _silver = silver;
        OnSilverChanged?.Invoke(silver);
    }

    public IReadOnlyCollection<MarketOrder> GetMarketOrders() => _marketOrders.Values.ToArray();
    public IReadOnlyCollection<InventoryItem> GetInventory() => _inventory.Values.ToArray();
    public PlayerPosition GetPlayerPosition() => _position;
    public SilverUpdate GetSilverUpdate() => _silver;
}
