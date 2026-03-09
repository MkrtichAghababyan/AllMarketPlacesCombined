using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AllMarketPlacesCombined.Models.WBModels
{
    public sealed class WbAnalyticsSalesSummary
    {
        public string OfferId { get; set; } = string.Empty;
        public long NmId { get; set; }
        public Dictionary<string, int> Days { get; set; } = new();
        public int Total7 { get; set; }
        public decimal AvgPerDay { get; set; }
    }
}
