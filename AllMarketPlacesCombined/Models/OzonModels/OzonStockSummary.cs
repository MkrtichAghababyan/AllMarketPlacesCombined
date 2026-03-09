using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AllMarketPlacesCombined.Models.OzonModels
{
    public sealed class OzonStockSummary
    {
        public string OfferId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public HashSet<long> Skus { get; set; } = new();

        // Combined Totals
        public int QuantityWarehouseTotal { get; set; }
        public int QuantityFullTotal { get; set; }
        public int ReservedTotal { get; set; }
        public int PromisedTotal { get; set; }

        // Explicit FBO/FBS Tracking
        public int FboPresent { get; set; }
        public int FbsPresent { get; set; }
    }
}
