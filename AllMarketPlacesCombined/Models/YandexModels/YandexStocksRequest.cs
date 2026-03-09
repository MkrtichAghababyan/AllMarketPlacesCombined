using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace AllMarketPlacesCombined.Models.YandexModels
{
    public class YandexStocksRequest
    {
        [JsonPropertyName("archived")]
        public bool Archived { get; set; } = false;

        [JsonPropertyName("withTurnover")]
        public bool WithTurnover { get; set; } = false;

        [JsonPropertyName("page_token")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? PageToken { get; set; }
    }

    public class YandexStocksResponse
    {
        [JsonPropertyName("status")]
        public string Status { get; set; }

        [JsonPropertyName("result")]
        public YandexStocksResult Result { get; set; }
    }

    public class YandexStocksResult
    {
        [JsonPropertyName("paging")]
        public YandexPaging Paging { get; set; }

        // FIXED: Yandex nests offers inside warehouses!
        [JsonPropertyName("warehouses")]
        public List<YandexWarehouse> Warehouses { get; set; }
    }

    public class YandexPaging
    {
        [JsonPropertyName("nextPageToken")]
        public string? NextPageToken { get; set; }
    }

    // NEW: We need to represent the Warehouse level
    public class YandexWarehouse
    {
        [JsonPropertyName("warehouseId")]
        public long WarehouseId { get; set; }

        [JsonPropertyName("offers")]
        public List<YandexOffer> Offers { get; set; }
    }

    public class YandexOffer
    {
        [JsonPropertyName("offerId")]
        public string OfferId { get; set; }

        [JsonPropertyName("stocks")]
        public List<YandexStock> Stocks { get; set; }
    }

    public class YandexStock
    {
        [JsonPropertyName("type")]
        public string Type { get; set; }

        [JsonPropertyName("count")]
        public int Count { get; set; }
    }

    public class YandexProductSummary
    {
        public string OfferId { get; set; }
        public int Stock { get; set; }
        public int Total7 { get; set; } // We will use this later for sales!
    }
}
