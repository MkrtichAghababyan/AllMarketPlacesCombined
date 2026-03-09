using AllMarketPlacesCombined.Models.WBModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Threading.Tasks;
using static AllMarketPlacesCombined.Models.WBModels.WbSalesFunnelHistoryRequest;

namespace AllMarketPlacesCombined.Services.WbServices
{
    public class WbAnalyticsService
    {
        private readonly string _token;
        private readonly HttpClient _http;

        public WbAnalyticsService(string token, HttpClient http)
        {
            _token = token;
            _http = http;
        }

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        private static WbSalesFunnelPeriodRequest BuildRequest(DateTime rangeFrom, DateTime rangeToInclusive)
        {
            var dayCount = (rangeToInclusive.Date - rangeFrom.Date).Days + 1;
            var pastEnd = rangeFrom.Date.AddDays(-1);
            var pastStart = pastEnd.AddDays(-(dayCount - 1));

            return new WbSalesFunnelPeriodRequest
            {
                SelectedPeriod = new WbPeriod
                {
                    Start = rangeFrom.ToString("yyyy-MM-dd"),
                    End = rangeToInclusive.ToString("yyyy-MM-dd")
                },
                PastPeriod = new WbPeriod
                {
                    Start = pastStart.ToString("yyyy-MM-dd"),
                    End = pastEnd.ToString("yyyy-MM-dd")
                },
                NmIds = Array.Empty<long>(),
                BrandNames = Array.Empty<string>(),
                SubjectIds = Array.Empty<long>(),
                TagIds = Array.Empty<long>(),
                SkipDeletedNm = false,
                OrderBy = new WbOrderBy
                {
                    Field = "openCard",
                    Mode = "asc"
                },
                Limit = 1000,
                Offset = 0,
                Timezone = "Europe/Moscow"
            };
        }

        public async Task<List<WbAnalyticsOfferSummary>> GetTotalsAndDaysForAllOffersAsync(
            DateTime rangeFrom,
            DateTime rangeToInclusive,
            CancellationToken cancellationToken = default)
        {
            // 1. Get totals for ALL products (leaves NmIds empty)
            var totals = await GetPeriodTotalsAsync(rangeFrom, rangeToInclusive, cancellationToken);

            // 2. Extract all NmIds we just found to query their history
            var allNmIds = totals.Where(t => t.NmId > 0).Select(t => t.NmId).ToArray();

            var daily = new List<WbAnalyticsOfferSummary>();

            // 3. The /history API only allows a max of 20 NmIds per request. We must chunk them.
            var chunkedNmIds = allNmIds.Chunk(20).ToList();

            for (int i = 0; i < chunkedNmIds.Count; i++)
            {
                var chunk = chunkedNmIds[i];
                Console.WriteLine($"Fetching history for chunk {i + 1} of {chunkedNmIds.Count}...");

                var historyChunk = await GetDailyHistoryAsync(rangeFrom, rangeToInclusive, chunk, cancellationToken);
                daily.AddRange(historyChunk);

                // If it's NOT the last chunk, we MUST wait 21 seconds before asking Wildberries for more
                if (i < chunkedNmIds.Count - 1)
                {
                    Console.WriteLine("Waiting 21 seconds to respect Wildberries rate limits...");
                    await Task.Delay(TimeSpan.FromSeconds(21), cancellationToken);
                }
            }

            // 4. Merge totals + daily by OfferId
            var map = new Dictionary<string, WbAnalyticsOfferSummary>(StringComparer.OrdinalIgnoreCase);

            foreach (var item in totals)
            {
                map[item.OfferId] = item;
            }

            foreach (var item in daily)
            {
                if (!map.TryGetValue(item.OfferId, out var existing))
                {
                    map[item.OfferId] = item;
                    continue;
                }

                foreach (var d in item.Days)
                    existing.Days[d.Key] = d.Value;

                existing.Total7 = item.Days.Values.Sum();
                existing.AvgPerDay = existing.Total7 / 7m;
            }

            foreach (var item in map.Values)
            {
                if (item.Days.Count == 0)
                {
                    for (var d = rangeFrom.Date; d <= rangeToInclusive.Date; d = d.AddDays(1))
                        item.Days[d.ToString("yyyy-MM-dd")] = 0;
                }

                item.Total7 = item.Days.Values.Sum();
                item.AvgPerDay = item.Total7 / 7m;
            }

            return map.Values
                .OrderBy(x => x.OfferId)
                .ToList();
        }

        public async Task<List<WbAnalyticsOfferSummary>> GetPeriodTotalsAsync(
            DateTime rangeFrom,
            DateTime rangeToInclusive,
            CancellationToken cancellationToken = default)
        {
            var endpoint = "https://seller-analytics-api.wildberries.ru/api/analytics/v3/sales-funnel/products";
            var payload = BuildRequest(rangeFrom, rangeToInclusive);
            var requestJson = JsonSerializer.Serialize(payload, JsonOptions);

            int maxRetries = 5;
            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                // We recreate the HttpRequestMessage inside the loop because HttpClient 
                // doesn't allow sending the exact same request object twice!
                using var req = new HttpRequestMessage(HttpMethod.Post, endpoint);
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token);
                req.Content = new StringContent(requestJson, Encoding.UTF8, "application/json");

                using var resp = await _http.SendAsync(req, cancellationToken).ConfigureAwait(false);
                var body = await resp.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

                // --- 429 AUTO-RETRY LOGIC ---
                if ((int)resp.StatusCode == 429)
                {
                    Console.WriteLine($"   [WB Внимание] Слишком много запросов (429). Ждем 60 секунд... (Попытка {attempt}/{maxRetries})");
                    await Task.Delay(TimeSpan.FromSeconds(60), cancellationToken);
                    continue; // Skip the rest of the loop and try again!
                }

                if (!resp.IsSuccessStatusCode)
                    throw new InvalidOperationException($"WB analytics /products error: {(int)resp.StatusCode}. {body}");

                var parsed = JsonSerializer.Deserialize<WbProductsResponse>(body, JsonOptions);
                var result = new List<WbAnalyticsOfferSummary>();

                if (parsed?.Data?.Products == null)
                    return result;

                foreach (var p in parsed.Data.Products)
                {
                    if (p.Product == null || p.Statistic?.Selected == null) continue;

                    var offerId = p.Product.VendorCode ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(offerId)) continue;

                    int totalStock = p.Product.Stocks != null ? p.Product.Stocks.Wb + p.Product.Stocks.Mp : 0;

                    result.Add(new WbAnalyticsOfferSummary
                    {
                        OfferId = offerId,
                        NmId = p.Product.NmId,
                        Name = p.Product.Title ?? string.Empty,
                        TotalStock = totalStock,
                        Days = new Dictionary<string, int>(),
                        Total7 = p.Statistic.Selected.OrderCount,
                        AvgPerDay = p.Statistic.Selected.OrderCount / 7m
                    });
                }
                return result;
            }

            throw new InvalidOperationException("WB API: Превышен лимит ожидания после нескольких ошибок 429.");
        }

        private static WbSalesFunnelHistoryRequest BuildHistoryRequest(DateTime rangeFrom, DateTime rangeToInclusive, long[] nmIds)
        {
            return new WbSalesFunnelHistoryRequest
            {
                SelectedPeriod = new WbPeriodHistory
                {
                    Start = rangeFrom.ToString("yyyy-MM-dd"),
                    End = rangeToInclusive.ToString("yyyy-MM-dd")
                },
                NmIds = nmIds,
                SkipDeletedNm = false,
                AggregationLevel = "day",
            };
        }

        public async Task<List<WbAnalyticsOfferSummary>> GetDailyHistoryAsync(
            DateTime rangeFrom,
            DateTime rangeToInclusive,
            long[] nmIds,
            CancellationToken cancellationToken = default)
        {
            var endpoint = "https://seller-analytics-api.wildberries.ru/api/analytics/v3/sales-funnel/products/history";
            var payload = BuildHistoryRequest(rangeFrom, rangeToInclusive, nmIds);
            var requestJson = JsonSerializer.Serialize(payload, JsonOptions);

            int maxRetries = 5;
            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                using var req = new HttpRequestMessage(HttpMethod.Post, endpoint);
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token);
                req.Content = new StringContent(requestJson, Encoding.UTF8, "application/json");

                using var resp = await _http.SendAsync(req, cancellationToken).ConfigureAwait(false);
                var body = await resp.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

                // --- 429 AUTO-RETRY LOGIC ---
                if ((int)resp.StatusCode == 429)
                {
                    Console.WriteLine($"   [WB Внимание] Слишком много запросов истории (429). Ждем 60 секунд... (Попытка {attempt}/{maxRetries})");
                    await Task.Delay(TimeSpan.FromSeconds(60), cancellationToken);
                    continue; // Skip the rest of the loop and try again!
                }

                if (!resp.IsSuccessStatusCode)
                    throw new InvalidOperationException($"WB analytics /products/history error: {(int)resp.StatusCode}. {body}");

                return ParseProductsHistory(body, rangeFrom, rangeToInclusive);
            }

            throw new InvalidOperationException("WB API: Превышен лимит ожидания истории после нескольких ошибок 429.");
        }

        private static List<WbAnalyticsOfferSummary> ParseProductsHistory(
            string json,
            DateTime rangeFrom,
            DateTime rangeToInclusive)
        {
            using var doc = JsonDocument.Parse(json);
            var result = new List<WbAnalyticsOfferSummary>();

            // 1. The root element itself IS the array in the /history response!
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
                return result;

            // 2. We enumerate directly over the root element
            foreach (var item in doc.RootElement.EnumerateArray())
            {
                if (!item.TryGetProperty("product", out var productEl))
                    continue;

                string offerId = productEl.TryGetProperty("vendorCode", out var vendorCodeEl)
                    ? vendorCodeEl.GetString() ?? string.Empty
                    : string.Empty;

                if (string.IsNullOrWhiteSpace(offerId))
                    continue;

                long nmId = productEl.TryGetProperty("nmId", out var nmIdEl) && nmIdEl.ValueKind == JsonValueKind.Number
                    ? nmIdEl.GetInt64()
                    : 0;

                string name = productEl.TryGetProperty("title", out var titleEl)
                    ? titleEl.GetString() ?? string.Empty
                    : string.Empty;

                var summary = new WbAnalyticsOfferSummary
                {
                    OfferId = offerId,
                    NmId = nmId,
                    Name = name,
                    Days = new Dictionary<string, int>()
                };

                // Заполняем все 7 дней нулями заранее
                for (var d = rangeFrom.Date; d <= rangeToInclusive.Date; d = d.AddDays(1))
                    summary.Days[d.ToString("yyyy-MM-dd")] = 0;

                // В ответе history обычно есть массив с днями/неделями.
                JsonElement historyEl;
                bool hasHistory =
                    item.TryGetProperty("history", out historyEl) ||
                    item.TryGetProperty("selected", out historyEl) ||
                    item.TryGetProperty("statistics", out historyEl);

                if (hasHistory)
                {
                    // Внутри ищем массивы с периодами по дням.
                    ExtractHistoryDays(historyEl, summary.Days);
                }

                summary.Total7 = summary.Days.Values.Sum();
                summary.AvgPerDay = summary.Total7 / 7m;

                result.Add(summary);
            }

            return result;
        }

        private static void ExtractHistoryDays(JsonElement element, Dictionary<string, int> days)
        {
            // Рекурсивно ищем объекты с period/date и orderCount
            if (element.ValueKind == JsonValueKind.Array)
            {
                foreach (var child in element.EnumerateArray())
                    ExtractHistoryDays(child, days);

                return;
            }

            if (element.ValueKind != JsonValueKind.Object)
                return;

            // Попытка распознать дневную запись
            string? day = null;
            int? orderCount = null;

            if (element.TryGetProperty("date", out var dateEl) && dateEl.ValueKind == JsonValueKind.String)
                day = dateEl.GetString();

            if (element.TryGetProperty("period", out var periodEl) &&
                periodEl.ValueKind == JsonValueKind.Object &&
                periodEl.TryGetProperty("start", out var startEl) &&
                startEl.ValueKind == JsonValueKind.String)
            {
                day = startEl.GetString();
            }

            if (element.TryGetProperty("orderCount", out var orderCountEl) &&
                orderCountEl.ValueKind == JsonValueKind.Number)
            {
                orderCount = orderCountEl.GetInt32();
            }

            if (!string.IsNullOrWhiteSpace(day) &&
                DateTime.TryParse(day, out var parsedDay) &&
                orderCount.HasValue)
            {
                var key = parsedDay.ToString("yyyy-MM-dd");
                if (days.ContainsKey(key))
                    days[key] = orderCount.Value;
            }

            foreach (var prop in element.EnumerateObject())
                ExtractHistoryDays(prop.Value, days);
        }

        public async Task WriteSummaryJsonAsync(
             List<WbAnalyticsOfferSummary> data,
             string outPath,
             DateTime rangeFrom,
             DateTime rangeToInclusive,
             CancellationToken cancellationToken = default)
        {
            // Calculate the total items sold over the 7 days
            var totalItemsSold = data.Sum(x => x.Total7);

            // Create the new payload with TotalSummary at the top
            var payload = new
            {
                TotalSummary = new
                {
                    TotalItemsSold7Days = totalItemsSold
                },
                ReportDates = new
                {
                    RangeFrom = rangeFrom.ToString("yyyy-MM-dd"),
                    RangeTo = rangeToInclusive.ToString("yyyy-MM-dd")
                },
                Products = data.OrderBy(x => x.OfferId).ToList()
            };

            await File.WriteAllTextAsync(
                outPath,
                JsonSerializer.Serialize(payload, JsonOptions),
                Encoding.UTF8,
                cancellationToken);
        }
    }
}