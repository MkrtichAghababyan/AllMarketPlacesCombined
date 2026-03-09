using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace AllMarketPlacesCombined.Models.WBModels
{
    public sealed class WbSalesFunnelPeriodRequest
    {
        [JsonPropertyName("selectedPeriod")]
        public WbPeriod SelectedPeriod { get; set; } = new();

        [JsonPropertyName("pastPeriod")]
        public WbPeriod PastPeriod { get; set; } = new();

        [JsonPropertyName("nmIds")]
        public long[] NmIds { get; set; } = Array.Empty<long>();

        [JsonPropertyName("brandNames")]
        public string[] BrandNames { get; set; } = Array.Empty<string>();

        [JsonPropertyName("subjectIds")]
        public long[] SubjectIds { get; set; } = Array.Empty<long>();

        [JsonPropertyName("tagIds")]
        public long[] TagIds { get; set; } = Array.Empty<long>();

        [JsonPropertyName("skipDeletedNm")]
        public bool SkipDeletedNm { get; set; } = false;

        [JsonPropertyName("orderBy")]
        public WbOrderBy OrderBy { get; set; } = new();

        [JsonPropertyName("limit")]
        public int Limit { get; set; } = 1000;

        [JsonPropertyName("offset")]
        public int Offset { get; set; } = 0;

        // Для history
        [JsonPropertyName("timezone")]
        public string? Timezone { get; set; }
    }

    public sealed class WbPeriod
    {
        [JsonPropertyName("start")]
        public string Start { get; set; } = "";

        [JsonPropertyName("end")]
        public string End { get; set; } = "";
    }

    public sealed class WbOrderBy
    {
        [JsonPropertyName("field")]
        public string Field { get; set; } = "openCard";

        [JsonPropertyName("mode")]
        public string Mode { get; set; } = "asc";
    }
}
