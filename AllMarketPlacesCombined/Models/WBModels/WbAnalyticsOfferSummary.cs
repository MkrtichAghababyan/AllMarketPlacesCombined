using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AllMarketPlacesCombined.Models.WBModels
{
    public sealed class WbAnalyticsOfferSummary
    {
        public string OfferId { get; set; } = "";
        public long NmId { get; set; }
        public string Name { get; set; } = "";
        public Dictionary<string, int> Days { get; set; } = new();
        public int TotalStock { get; set; }
        public int Total7 { get; set; }
        public decimal AvgPerDay { get; set; }
    }
}
