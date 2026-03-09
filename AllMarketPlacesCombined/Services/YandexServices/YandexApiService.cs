using AllMarketPlacesCombined.Models.YandexModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace AllMarketPlacesCombined.Services.YandexServices
{
    public class YandexApiService
    {
        private readonly string _token;
        private readonly HttpClient _http;

        public YandexApiService(string token)
        {
            _token = token;
            _http = new HttpClient();
            _http.DefaultRequestHeaders.Add("Api-Key", _token);
        }

        // 1. NEW: Fetch all stores associated with your token
        public async Task<List<YandexCampaign>> GetCampaignsAsync()
        {
            var endpoint = "https://api.partner.market.yandex.ru/v2/campaigns.json";
            var response = await _http.GetAsync(endpoint);
            var responseBody = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"Yandex API Error (Campaigns): {response.StatusCode} - {responseBody}");
            }

            var data = JsonSerializer.Deserialize<YandexCampaignsResponse>(responseBody);
            return data?.Campaigns ?? new List<YandexCampaign>();
        }

        // 2. UPDATED: Accepts the specific campaignId AND the master list to combine stocks
        public async Task GetStocksAsync(string campaignId, List<YandexReportItem> masterList)
        {
            var endpoint = $"https://api.partner.market.yandex.ru/v2/campaigns/{campaignId}/offers/stocks.json";
            string? nextPageToken = null;

            do
            {
                var payload = new YandexStocksRequest
                {
                    Archived = false,
                    WithTurnover = false,
                    PageToken = nextPageToken
                };

                var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
                var response = await _http.PostAsync(endpoint, content);
                var responseBody = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    // Some campaigns might not support stocks (like pure DBS), so we just safely ignore the error
                    Console.WriteLine($"   [!] Пропуск стоков для кампании {campaignId}: {response.StatusCode}");
                    break;
                }

                var data = JsonSerializer.Deserialize<YandexStocksResponse>(responseBody);

                if (data?.Result?.Warehouses != null)
                {
                    foreach (var warehouse in data.Result.Warehouses)
                    {
                        if (warehouse.Offers != null)
                        {
                            foreach (var offer in warehouse.Offers)
                            {
                                var availableStock = offer.Stocks?.FirstOrDefault(s => s.Type == "AVAILABLE")?.Count ?? 0;

                                // Check if we already found this item in a previous campaign/store
                                var existingProduct = masterList.FirstOrDefault(p => p.OfferId.Equals(offer.OfferId, StringComparison.OrdinalIgnoreCase));

                                if (existingProduct != null)
                                {
                                    existingProduct.Stock += availableStock;
                                }
                                else
                                {
                                    masterList.Add(new YandexReportItem
                                    {
                                        OfferId = offer.OfferId,
                                        Stock = availableStock,
                                        Total7 = 0
                                    });
                                }
                            }
                        }
                    }
                }

                nextPageToken = data?.Result?.Paging?.NextPageToken;

            } while (!string.IsNullOrEmpty(nextPageToken));
        }

        // 3. UPDATED: Accepts the specific campaignId
        public async Task Get7DaySalesAsync(string campaignId, List<YandexReportItem> masterList)
        {
            var dateFrom = DateTime.Now.AddDays(-7).ToString("dd-MM-yyyy");
            var dateTo = DateTime.Now.ToString("dd-MM-yyyy");

            int currentPage = 1;
            int pagesCount = 1;

            do
            {
                var endpoint = $"https://api.partner.market.yandex.ru/v2/campaigns/{campaignId}/orders.json?fromDate={dateFrom}&toDate={dateTo}&page={currentPage}";

                var response = await _http.GetAsync(endpoint);
                var responseBody = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"   [!] Пропуск заказов для кампании {campaignId}: {response.StatusCode}");
                    break;
                }

                var data = JsonSerializer.Deserialize<YandexOrdersResponse>(responseBody);

                if (data?.Orders != null)
                {
                    foreach (var order in data.Orders)
                    {
                        if (order.Status == "CANCELLED") continue;

                        if (order.Items != null)
                        {
                            foreach (var item in order.Items)
                            {
                                var product = masterList.FirstOrDefault(p => p.OfferId.Equals(item.OfferId, StringComparison.OrdinalIgnoreCase));

                                if (product != null)
                                {
                                    product.Total7 += item.Count;
                                }
                                else
                                {
                                    masterList.Add(new YandexReportItem
                                    {
                                        OfferId = item.OfferId,
                                        Stock = 0,
                                        Total7 = item.Count
                                    });
                                }
                            }
                        }
                    }
                }

                if (data?.Pager != null)
                {
                    pagesCount = data.Pager.PagesCount;
                }

                currentPage++;

            } while (currentPage <= pagesCount);
        }
    }
}
