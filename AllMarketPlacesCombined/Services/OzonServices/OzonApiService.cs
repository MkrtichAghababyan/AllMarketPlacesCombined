using AllMarketPlacesCombined.Models.OzonModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace AllMarketPlacesCombined.Services.OzonServices
{
    public sealed class OzonApiService
    {
        private const string BaseUrl = "https://api-seller.ozon.ru/";
        private readonly HttpClient _http;
        private readonly string _clientId;
        private readonly string _apiKey;

        public OzonApiService(string clientId, string apiKey)
        {
            _clientId = clientId ?? throw new ArgumentNullException(nameof(clientId));
            _apiKey = apiKey ?? throw new ArgumentNullException(nameof(apiKey));
            _http = new HttpClient { BaseAddress = new Uri(BaseUrl), Timeout = TimeSpan.FromSeconds(60) };
        }

        public async Task<OzonStockResult> GetStocksAsync(CancellationToken cancellationToken = default)
        {
            var products = new Dictionary<string, OzonStockSummary>(StringComparer.OrdinalIgnoreCase);
            var skuToOfferId = new Dictionary<long, string>();

            // --- STEP 1: Master Product List (v4) ---
            string cursor = "";
            try
            {
                while (true)
                {
                    using var req = new HttpRequestMessage(HttpMethod.Post, "v4/product/info/stocks");
                    AddHeaders(req);
                    req.Content = CreateJsonContent(new { filter = new { visibility = "ALL" }, cursor, limit = 1000 });

                    using var resp = await _http.SendAsync(req, cancellationToken);
                    if (!resp.IsSuccessStatusCode) break;

                    var body = await resp.Content.ReadAsStringAsync(cancellationToken);
                    using var doc = JsonDocument.Parse(body);
                    var resultEl = doc.RootElement.GetProperty("result");

                    foreach (var item in resultEl.GetProperty("items").EnumerateArray())
                    {
                        var offerId = item.GetProperty("offer_id").GetString() ?? "";
                        if (string.IsNullOrEmpty(offerId)) continue;

                        if (!products.ContainsKey(offerId))
                            products[offerId] = new OzonStockSummary { OfferId = offerId };

                        if (item.TryGetProperty("stocks", out var stocksEl))
                        {
                            foreach (var s in stocksEl.EnumerateArray())
                            {
                                if (s.TryGetProperty("sku", out var skuProp))
                                {
                                    long sku = skuProp.GetInt64();
                                    products[offerId].Skus.Add(sku);
                                    skuToOfferId[sku] = offerId;
                                }
                            }
                        }
                    }
                    cursor = resultEl.TryGetProperty("cursor", out var c) ? c.GetString() ?? "" : "";
                    if (string.IsNullOrEmpty(cursor)) break;
                }
            }
            catch { /* Continue to step 2 even if v4 fails */ }

            // --- STEP 2: Enrich with Warehouse Analytics (v2) ---
            int totalAvailable = 0;
            int totalPresent = 0;

            using var reqWh = new HttpRequestMessage(HttpMethod.Post, "v2/analytics/stock_on_warehouses");
            AddHeaders(reqWh);
            reqWh.Content = CreateJsonContent(new { limit = 1000, offset = 0, warehouse_type = "ALL" });

            using var respWh = await _http.SendAsync(reqWh, cancellationToken);
            if (respWh.IsSuccessStatusCode)
            {
                var bodyWh = await respWh.Content.ReadAsStringAsync(cancellationToken);
                using var docWh = JsonDocument.Parse(bodyWh);
                var rows = docWh.RootElement.GetProperty("result").GetProperty("rows");

                foreach (var row in rows.EnumerateArray())
                {
                    var offerId = row.GetProperty("item_code").GetString() ?? "";
                    if (string.IsNullOrEmpty(offerId)) continue;

                    // FALLBACK: If not in Step 1, add it now!
                    if (!products.TryGetValue(offerId, out var summary))
                    {
                        summary = new OzonStockSummary { OfferId = offerId };
                        products[offerId] = summary;
                    }

                    int free = row.GetProperty("free_to_sell_amount").GetInt32();
                    int reserved = row.GetProperty("reserved_amount").GetInt32();
                    int promised = row.GetProperty("promised_amount").GetInt32();
                    int present = free + reserved + promised;

                    summary.QuantityFullTotal += free;
                    summary.QuantityWarehouseTotal += present;
                    summary.ReservedTotal += reserved;

                    if (string.IsNullOrEmpty(summary.Name))
                        summary.Name = row.GetProperty("item_name").GetString() ?? "";

                    // --- ADD THE FBO/FBS CHECK HERE ---
                    string type = row.TryGetProperty("warehouse_type", out var t) ? t.GetString() ?? "" : "";
                    if (type.Equals("ALL", StringComparison.OrdinalIgnoreCase) || type.Equals("FBO", StringComparison.OrdinalIgnoreCase))
                    {
                        summary.FboPresent += present;
                    }
                    else
                    {
                        summary.FbsPresent += present;
                    }
                    // ----------------------------------

                    totalAvailable += free;
                    totalPresent += present;
                }
            }

            return new OzonStockResult { Products = products, SkuToOfferId = skuToOfferId, TotalAvailable = totalAvailable, TotalPresent = totalPresent };
        }

        public async Task<List<ProductSalesReport>> GetSalesReportAsync(
        DateTime ignoredDate,
        string fulfillment,
        Dictionary<string, OzonStockSummary> stockByOfferId,
        Dictionary<long, string> skuToOfferId,
        CancellationToken cancellationToken = default)
        {
            var endpoint = fulfillment.Equals("FBS", StringComparison.OrdinalIgnoreCase) ? "v3/posting/fbs/list" : "v2/posting/fbo/list";

            // DYNAMIC DATE CALCULATION
            // If Today is 08.03:
            // 'to' becomes 07.03 at 23:59:59
            // 'since' becomes 01.03 at 00:00:00
            var now = DateTime.UtcNow;
            var toDate = now.Date.AddDays(-1);
            var sinceDate = now.Date.AddDays(-7);

            var since = sinceDate.ToString("yyyy-MM-ddTHH:mm:ssZ");
            var to = toDate.ToString("yyyy-MM-ddT23:59:59Z");

            var mergedSales = new Dictionary<string, OzonSalesSummary>(StringComparer.OrdinalIgnoreCase);
            int offset = 0;

            while (true)
            {
                using var req = new HttpRequestMessage(HttpMethod.Post, endpoint);
                AddHeaders(req);
                req.Content = CreateJsonContent(new
                {
                    dir = "ASC",
                    filter = new { since, to },
                    limit = 1000,
                    offset
                });

                using var resp = await _http.SendAsync(req, cancellationToken);
                if (!resp.IsSuccessStatusCode) break;

                var body = await resp.Content.ReadAsStringAsync(cancellationToken);
                var (pageSales, postingsCount) = ParseOzonLast7DaysSales(body, skuToOfferId);

                // ... rest of your existing merging logic ...
                foreach (var item in pageSales)
                {
                    if (!mergedSales.TryGetValue(item.OfferId, out var existing))
                        mergedSales[item.OfferId] = item;
                    else
                    {
                        foreach (var day in item.Days)
                            existing.Days[day.Key] = existing.Days.TryGetValue(day.Key, out var cur) ? cur + day.Value : day.Value;
                        existing.Revenue7 += item.Revenue7;
                    }
                }
                if (postingsCount < 1000) break;
                offset += 1000;
            }

            // Final Assembly
            var reports = new List<ProductSalesReport>();

            // Change -6 to -7 to start exactly 7 days ago
            var startDay = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(-7));

            foreach (var product in mergedSales.Values.OrderBy(x => x.OfferId))
            {
                var days = new Dictionary<string, int>();
                int total = 0;

                // This loop goes from 0 to 6, which adds up to 7 full days
                for (int i = 0; i < 7; i++)
                {
                    var dStr = startDay.AddDays(i).ToString("yyyy-MM-dd");
                    int q = product.Days.TryGetValue(dStr, out var v) ? v : 0;

                    days[dStr] = q;
                    total += q;
                }

                int stock = stockByOfferId.TryGetValue(product.OfferId, out var s) ? s.QuantityFullTotal : 0;

                reports.Add(new ProductSalesReport
                {
                    OfferId = product.OfferId,
                    Name = product.Name,
                    Stock = stock,
                    Days = days,             // <-- FIX: Use the local 'days' dictionary you just built
                    Total7 = total,          // <-- FIX: Use the local 'total' you just calculated
                    AvgPerDay = total / 7m,  // <-- FIX: Calculate the average using the new total
                    Revenue7 = product.Revenue7
                });
            }

            return reports;
        }


        public static (List<OzonSalesSummary> Summaries, int PostingsCount) ParseOzonLast7DaysSales(string json, Dictionary<long, string>? skuToOfferId = null)
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("result", out var res)) return (new(), 0);

            JsonElement postings;
            if (res.ValueKind == JsonValueKind.Array) postings = res;
            else if (res.TryGetProperty("postings", out var p)) postings = p;
            else return (new(), 0);

            int count = postings.GetArrayLength();
            var sales = new Dictionary<string, OzonSalesSummary>(StringComparer.OrdinalIgnoreCase);

            // --- FIX THIS LINE ---
            // Change -6 to -7 to capture the full 7-day window.
            var start = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(-7));

            foreach (var post in postings.EnumerateArray())
            {
                if (!post.TryGetProperty("created_at", out var c) || !DateTimeOffset.TryParse(c.GetString(), out var dt)) continue;
                var day = DateOnly.FromDateTime(dt.UtcDateTime.Date);
                if (day < start) continue;

                if (post.TryGetProperty("products", out var prods))
                {
                    foreach (var pr in prods.EnumerateArray())
                    {
                        string oId = pr.TryGetProperty("offer_id", out var o) ? o.GetString() ?? "" : "";
                        long sku = pr.TryGetProperty("sku", out var s) ? s.GetInt64() : 0;

                        // Fallback to Sku mapping if OfferId is empty
                        if (string.IsNullOrEmpty(oId) && sku != 0 && skuToOfferId != null)
                            skuToOfferId.TryGetValue(sku, out oId);

                        if (string.IsNullOrEmpty(oId)) oId = sku.ToString();

                        if (!sales.TryGetValue(oId, out var summ))
                        {
                            summ = new OzonSalesSummary { OfferId = oId, Name = pr.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "" };
                            sales[oId] = summ;
                        }

                        int q = pr.TryGetProperty("quantity", out var qp) ? qp.GetInt32() : 0;
                        decimal price = 0;
                        if (pr.TryGetProperty("price", out var pp))
                        {
                            if (pp.ValueKind == JsonValueKind.String) decimal.TryParse(pp.GetString(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out price);
                            else price = pp.GetDecimal();
                        }

                        var dKey = day.ToString("yyyy-MM-dd");
                        summ.Days[dKey] = summ.Days.TryGetValue(dKey, out var cur) ? cur + q : q;
                        summ.Revenue7 += (q * price);
                    }
                }
            }
            return (sales.Values.ToList(), count);
        }

        private void AddHeaders(HttpRequestMessage req)
        {
            req.Headers.Add("Client-Id", _clientId);
            req.Headers.Add("Api-Key", _apiKey);
            req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }

        private static StringContent CreateJsonContent<T>(T payload) => new(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
    }
}
