using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace AllMarketPlacesCombined.Models.WBModels
{
    public sealed class WbSalesFunnelHistoryRequest
    {
        [JsonPropertyName("selectedPeriod")]
        public WbPeriodHistory SelectedPeriod { get; set; } = new();

        [JsonPropertyName("nmIds")]
        public long[] NmIds { get; set; } = Array.Empty<long>();

        [JsonPropertyName("brandNames")]
        public string[] BrandNames { get; set; } = Array.Empty<string>();

        [JsonPropertyName("subjectIds")]
        public long[] SubjectIds { get; set; } = Array.Empty<long>();

        [JsonPropertyName("skipDeletedNm")]
        public bool SkipDeletedNm { get; set; } = false;

        [JsonPropertyName("aggregationLevel")]
        public string AggregationLevel { get; set; } = "day";

        [JsonPropertyName("tagIds")]
        public long[] TagIds { get; set; } = Array.Empty<long>();

        [JsonPropertyName("limit")]
        public int Limit { get; set; } = 1000;

        [JsonPropertyName("offset")]
        public int Offset { get; set; } = 0;

        public sealed class WbPeriodHistory
        {
            [JsonPropertyName("start")]
            public string Start { get; set; } = "";

            [JsonPropertyName("end")]
            public string End { get; set; } = "";
        }
    }
}
