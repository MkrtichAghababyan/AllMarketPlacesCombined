using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace AllMarketPlacesCombined.Models.YandexModels
{
    public class YandexCampaignsResponse
    {
        [JsonPropertyName("campaigns")]
        public List<YandexCampaign> Campaigns { get; set; }
    }

    public class YandexCampaign
    {
        [JsonPropertyName("id")]
        public long Id { get; set; }

        // Domain usually holds the name or type of the store
        [JsonPropertyName("domain")]
        public string Domain { get; set; }
    }

    public class YandexReportItem
    {
        public string OfferId { get; set; }
        public int Stock { get; set; }
        public int Total7 { get; set; }
    }

    // --- NEW ORDER MODELS ---
    public class YandexOrdersResponse
    {
        [JsonPropertyName("orders")]
        public List<YandexOrder> Orders { get; set; }

        [JsonPropertyName("pager")]
        public YandexPager Pager { get; set; }
    }

    public class YandexPager
    {
        [JsonPropertyName("currentPage")]
        public int CurrentPage { get; set; }

        [JsonPropertyName("pagesCount")]
        public int PagesCount { get; set; }
    }

    public class YandexOrder
    {
        [JsonPropertyName("status")]
        public string Status { get; set; }

        [JsonPropertyName("items")]
        public List<YandexOrderItem> Items { get; set; }
    }

    public class YandexOrderItem
    {
        [JsonPropertyName("offerId")]
        public string OfferId { get; set; }

        [JsonPropertyName("count")]
        public int Count { get; set; }
    }
}
