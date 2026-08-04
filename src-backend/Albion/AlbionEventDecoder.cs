using System;
using System.Collections.Generic;
using AlbionBot.Models;
using AlbionBot.Protocol;

namespace AlbionBot.Albion;

public static class AlbionEventDecoder
{
    public static MarketOrder? DecodeMarketOrder(IDictionary<byte, object?> parameters)
    {
        if (!TryGetValue(parameters, 1, out var orderIdObj) || !TryGetValue(parameters, 2, out var itemIdObj))
        {
            return null;
        }

        var orderId = Convert.ToInt64(orderIdObj);
        var itemTypeId = itemIdObj?.ToString() ?? string.Empty;
        var price = parameters.TryGetValue(3, out var priceObj) ? Convert.ToInt64(priceObj) : 0;
        var quantity = parameters.TryGetValue(4, out var quantityObj) ? Convert.ToInt32(quantityObj) : 0;
        var quality = parameters.TryGetValue(5, out var qualityObj) ? Convert.ToByte(qualityObj) : (byte)0;
        var expiration = parameters.TryGetValue(6, out var expiresObj) ? Convert.ToInt64(expiresObj) : 0;
        var type = parameters.TryGetValue(7, out var typeObj) ? typeObj?.ToString() ?? "sell" : "sell";

        return new MarketOrder(orderId, itemTypeId, price, quantity, quality, expiration, type);
    }

    public static SilverUpdate? DecodeSilverUpdate(IDictionary<byte, object?> parameters)
    {
        var silver = parameters.TryGetValue(10, out var silverObj) ? Convert.ToInt64(silverObj) : 0;
        var gold = parameters.TryGetValue(11, out var goldObj) ? Convert.ToInt64(goldObj) : 0;
        return new SilverUpdate(silver, gold);
    }

    public static PlayerPosition? DecodePlayerPosition(IDictionary<byte, object?> parameters)
    {
        if (!TryGetValue(parameters, 20, out var xObj) || !TryGetValue(parameters, 21, out var yObj))
        {
            return null;
        }

        return new PlayerPosition(Convert.ToSingle(xObj), Convert.ToSingle(yObj));
    }

    public static IEnumerable<InventoryItem> DecodeInventoryItems(IDictionary<byte, object?> parameters)
    {
        if (!parameters.TryGetValue(30, out var inventoryObj) || inventoryObj is not object?[] inventoryArray)
        {
            yield break;
        }

        for (var i = 0; i < inventoryArray.Length; i += 4)
        {
            if (inventoryArray.Length <= i + 3)
            {
                break;
            }

            var slot = Convert.ToByte(inventoryArray[i]);
            var itemId = Convert.ToInt32(inventoryArray[i + 1]);
            var quantity = Convert.ToInt32(inventoryArray[i + 2]);
            var durability = Convert.ToSingle(inventoryArray[i + 3]);
            yield return new InventoryItem(slot, itemId, quantity, durability);
        }
    }

    private static bool TryGetValue(IDictionary<byte, object?> parameters, byte key, out object? value)
    {
        if (parameters.TryGetValue(key, out value) && value is not null)
        {
            return true;
        }

        value = null;
        return false;
    }
}
