using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AllMarketPlacesCombined.Models.OzonModels
{
    public sealed class ProductSalesReport
    {
        public string OfferId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public HashSet<long> Skus { get; set; } = new();
        public int Stock { get; set; }
        public Dictionary<string, int> Days { get; set; } = new();
        public int Total7 { get; set; }
        public decimal AvgPerDay { get; set; }
        public decimal Revenue7 { get; set; }
    }
}
