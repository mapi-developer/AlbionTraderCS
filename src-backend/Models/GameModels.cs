namespace AlbionBot.Models;

public record MarketOrder(
    long Id,
    string ItemTypeId,
    long UnitPrice,
    int Quantity,
    byte Quality,
    long ExpiresAt,
    string AuctionType // "buy" or "sell"
);

public record PlayerPosition(
    float X,
    float Y
);

public record InventoryItem(
    byte Slot,
    int ItemId,
    int Quantity,
    float Durability
);

public record SilverUpdate(
    long Silver,
    long Gold
);
