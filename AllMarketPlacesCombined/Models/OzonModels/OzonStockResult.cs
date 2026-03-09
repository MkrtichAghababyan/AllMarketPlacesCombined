using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AllMarketPlacesCombined.Models.OzonModels
{
    public sealed class OzonStockResult
    {
        public Dictionary<string, OzonStockSummary> Products { get; init; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<long, string> SkuToOfferId { get; init; } = new();
        public int TotalAvailable { get; set; }
        public int TotalPresent { get; set; }

    }
}
