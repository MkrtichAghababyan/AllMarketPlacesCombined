using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

// Your Project Namespaces
using AllMarketPlacesCombined.Models.OzonModels;
using AllMarketPlacesCombined.Models.WBModels;
using AllMarketPlacesCombined.Models.YandexModels;
using AllMarketPlacesCombined.Services.OzonServices;
using AllMarketPlacesCombined.Services.WbServices;
using AllMarketPlacesCombined.Services.YandexServices;
using AllMarketPlacesCombined.Services.GoogleSheetService;

namespace AllMarketPlacesCombined
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            // --- LOGGING SETUP ---
            List<string> syncLogs = new List<string>();
            Action<string> Log = (msg) => {
                string entry = $"[{DateTime.Now:HH:mm:ss}] {msg}";
                Console.WriteLine(entry);
                syncLogs.Add(entry);
            };

            Log("=== STARTING MARKETPLACE SYNC ===");

            // --- CONFIGURATION (GitHub Secrets or Local) ---
            string credentialsFilePath = "google-credentials.json";
            // Professional and Secure way
            string spreadsheetId = Environment.GetEnvironmentVariable("SHEET_ID") ?? "PASTE_ID_ONLY_FOR_LOCAL_TEST_THEN_DELETE";
            string sheetName = Environment.GetEnvironmentVariable("SHEET_NAME") ?? "Лист1";

            string wbToken = Environment.GetEnvironmentVariable("WB_TOKEN") ?? "";
            string ozonClientId = Environment.GetEnvironmentVariable("OZON_CLIENT_ID") ?? "";
            string ozonApiKey = Environment.GetEnvironmentVariable("OZON_API_KEY") ?? "";
            string yandexToken = Environment.GetEnvironmentVariable("YANDEX_TOKEN") ?? "";

            using var httpClient = new HttpClient();

            // --- SERVICE INITIALIZATION ---
            var googleSheets = new GoogleSheetsService(credentialsFilePath);
            var wbService = new WbAnalyticsService(wbToken, httpClient);
            var ozonService = new OzonApiService(ozonClientId, ozonApiKey);
            var yandexService = new YandexApiService(yandexToken);

            try
            {
                // 1. WILDBERRIES
                Log("[1/3] Fetching Wildberries Data...");
                DateTime to = DateTime.UtcNow.Date.AddDays(-1);
                DateTime from = to.AddDays(-6);
                var wbData = await wbService.GetTotalsAndDaysForAllOffersAsync(from, to);
                Log($"Found {wbData.Count} WB products. Updating Sheet...");
                await googleSheets.WriteWbDataAsync(spreadsheetId, sheetName, wbData);

                // 2. OZON
                Log("[2/3] Fetching Ozon Data...");
                var ozonStocks = await ozonService.GetStocksAsync();
                var fboSales = await ozonService.GetSalesReportAsync(DateTime.UtcNow, "FBO", ozonStocks.Products, ozonStocks.SkuToOfferId);
                var fbsSales = await ozonService.GetSalesReportAsync(DateTime.UtcNow, "FBS", ozonStocks.Products, ozonStocks.SkuToOfferId);

                // Merge FBO and FBS
                var ozonDict = new Dictionary<string, ProductSalesReport>(StringComparer.OrdinalIgnoreCase);
                foreach (var r in fboSales.Concat(fbsSales))
                {
                    if (!ozonDict.TryGetValue(r.OfferId, out var existing))
                        ozonDict[r.OfferId] = r;
                    else
                        existing.Total7 += r.Total7;
                }
                Log($"Found {ozonDict.Count} Ozon products. Updating Sheet...");
                await googleSheets.WriteOzonDataAsync(spreadsheetId, sheetName, ozonDict.Values.ToList());

                // 3. YANDEX
                Log("[3/3] Fetching Yandex Data...");
                var yandexList = new List<YandexReportItem>();
                var campaigns = await yandexService.GetCampaignsAsync();
                foreach (var camp in campaigns)
                {
                    await yandexService.GetStocksAsync(camp.Id.ToString(), yandexList);
                    await yandexService.Get7DaySalesAsync(camp.Id.ToString(), yandexList);
                }
                Log($"Found {yandexList.Count} Yandex products. Updating Sheet...");
                await googleSheets.WriteYandexDataAsync(spreadsheetId, sheetName, yandexList);

                Log("=== SYNC COMPLETED SUCCESSFULLY ===");
            }
            catch (Exception ex)
            {
                Log("!!! CRITICAL ERROR !!!");
                Log(ex.Message);
                Log(ex.StackTrace ?? "No stack trace available.");
            }
            finally
            {
                // Save the log file
                Log("Saving log file...");
                await File.WriteAllLinesAsync("sync_log.txt", syncLogs);
            }
        }
    }
}