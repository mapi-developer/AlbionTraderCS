using System;
using System.Text.Json.Serialization;

namespace AlbionBot.Core.Models;

public class MarketOrder
{
    [JsonPropertyName("Id")]
    public long Id { get; set; }

    [JsonPropertyName("ItemTypeId")]
    public string ItemTypeId { get; set; } = string.Empty;

    [JsonPropertyName("LocationId")]
    public int LocationId { get; set; }

    [JsonPropertyName("QualityLevel")]
    public int QualityLevel { get; set; }

    [JsonPropertyName("EnchantmentLevel")]
    public int EnchantmentLevel { get; set; }

    [JsonPropertyName("UnitPriceSilver")]
    public long UnitPriceSilver { get; set; }

    [JsonPropertyName("Amount")]
    public int Amount { get; set; }

    [JsonPropertyName("AuctionType")]
    public string AuctionType { get; set; } = string.Empty;

    [JsonPropertyName("Expires")]
    public DateTime Expires { get; set; }
}