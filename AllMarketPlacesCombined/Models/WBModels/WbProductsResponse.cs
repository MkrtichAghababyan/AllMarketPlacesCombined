using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace AllMarketPlacesCombined.Models.WBModels
{
    public sealed class WbProductsResponse
    {
        [JsonPropertyName("data")]
        public WbProductsData? Data { get; set; }
    }

    public sealed class WbProductsData
    {
        [JsonPropertyName("products")]
        public List<WbProductsItem>? Products { get; set; }
    }

    public sealed class WbProductsItem
    {
        [JsonPropertyName("product")]
        public WbProductInfo? Product { get; set; }

        [JsonPropertyName("statistic")]
        public WbProductStatistic? Statistic { get; set; }
    }

    public sealed class WbProductInfo
    {
        [JsonPropertyName("nmId")]
        public long NmId { get; set; }

        [JsonPropertyName("title")]
        public string? Title { get; set; }

        [JsonPropertyName("vendorCode")]
        public string? VendorCode { get; set; }

        [JsonPropertyName("stocks")]
        public WbStocks? Stocks { get; set; }
    }

    public sealed class WbStocks
    {
        [JsonPropertyName("wb")]
        public int Wb { get; set; } // Stock in Wildberries warehouses

        [JsonPropertyName("mp")]
        public int Mp { get; set; } // Stock in Seller's (Marketplace) warehouses

        [JsonPropertyName("balanceSum")]
        public int BalanceSum { get; set; } // Total combined stock
    }

    public sealed class WbProductStatistic
    {
        [JsonPropertyName("selected")]
        public WbSelectedStatistic? Selected { get; set; }
    }

    public sealed class WbSelectedStatistic
    {
        [JsonPropertyName("period")]
        public WbPeriod? Period { get; set; }

        [JsonPropertyName("orderCount")]
        public int OrderCount { get; set; }
    }
}
