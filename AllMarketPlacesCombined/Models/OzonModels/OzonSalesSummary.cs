using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AllMarketPlacesCombined.Models.OzonModels
{
    public sealed class OzonSalesSummary
    {
        public string OfferId { get; set; } = string.Empty;
        public long Sku { get; set; }
        public string Name { get; set; } = string.Empty;

        // Продажи по датам за последние 7 дней
        public Dictionary<string, int> Days { get; set; } = new();

        // Общая сумма за 7 дней
        public int Total7 { get; set; }

        // Среднее за 7 дней
        public decimal AvgPerDay { get; set; }
        public decimal Revenue7 { get; set; }
    }
}
