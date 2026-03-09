using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

// Importing your specific namespaces based on the files you provided
using AllMarketPlacesCombined.Models.OzonModels;
using AllMarketPlacesCombined.Models.WBModels;
using AllMarketPlacesCombined.Models.YandexModels;
using AllMarketPlacesCombined.Services.OzonServices;
using AllMarketPlacesCombined.Services.WbServices;
using AllMarketPlacesCombined.Services.YandexServices;
using AllMarketPlacesCombined.Services.GoogleSheetService; // Google Sheets namespace

namespace AllMarketPlacesCombined
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("=== ЗАПУСК ИНТЕГРАЦИИ МАРКЕТПЛЕЙСОВ ===");

            // ==========================================
            // 1. НАСТРОЙКИ И ТОКЕНЫ 
            // ==========================================

            // Google Sheets
            string credentialsFilePath = "google-credentials.json";
            string spreadsheetId = "1DIQ5Cqt-2jpJJmdjrNpba-HLhTNRJFeRu-heiN4wck4";
            string sheetName = "Лист1";

            // API Tokens
            string wbToken = "eyJhbGciOiJFUzI1NiIsImtpZCI6IjIwMjYwMzAydjEiLCJ0eXAiOiJKV1QifQ.eyJhY2MiOjEsImVudCI6MSwiZXhwIjoxNzg4NDEwMjAzLCJpZCI6IjAxOWNiOWI1LTc2NzAtN2E5MS05OTlmLTZkNmNhMDM0MjI0MCIsImlpZCI6MTYyNDAxMTQsIm9pZCI6MzkzODg0MywicyI6MTA3Mzc1Nzk1MCwic2lkIjoiOTkzNjZhMGEtN2Q0Ny00ODk0LThlZDUtOGNmODJjYzlkZDVmIiwidCI6ZmFsc2UsInVpZCI6MTYyNDAxMTR9.VC-aqiBKabhbSh8stWpthNG75h09czB_E1yAOrKswCjiayh3mmpGsWOKqZEftHdFvd7DRrxJy1cDepRGh0_ySA";
            string ozonClientId = "1527418";
            string ozonApiKey = "69f6c9a9-bcee-41c7-9303-3fabefd2e918";
            string yandexToken = "ACMA:fGgY4Rs0VXHz5hwj7Tj03025XcJtilYSkm6TwoI8:1af126e3";

            // Shared HttpClient for WB service to keep things fast
            using var sharedHttpClient = new HttpClient();

            // ==========================================
            // 2. ИНИЦИАЛИЗАЦИЯ СЕРВИСОВ
            // ==========================================

            // GOOGLE SHEETS ВАРИАНТ ТЕПЕРЬ АКТИВЕН!
            var googleSheetsService = new GoogleSheetsService(credentialsFilePath);

            var wbApiService = new WbAnalyticsService(wbToken, sharedHttpClient);
            var ozonApiService = new OzonApiService(ozonClientId, ozonApiKey);
            var yandexApiService = new YandexApiService(yandexToken);

            try
            {
                // ==========================================
                // 3. WILDBERRIES
                // ==========================================
                Console.WriteLine("\n[1/3] Загрузка данных Wildberries...");

                DateTime wbRangeTo = DateTime.UtcNow.Date.AddDays(-1);
                DateTime wbRangeFrom = wbRangeTo.AddDays(-6);

                var wbData = await wbApiService.GetTotalsAndDaysForAllOffersAsync(wbRangeFrom, wbRangeTo);
                Console.WriteLine($"Скачано товаров WB: {wbData.Count}");

                Console.WriteLine("Обновление Google Таблицы (Wildberries)...");
                await googleSheetsService.WriteWbDataAsync(spreadsheetId, sheetName, wbData);


                // ==========================================
                // 4. OZON
                // ==========================================
                Console.WriteLine("\n[2/3] Загрузка данных Ozon...");

                var ozonStocks = await ozonApiService.GetStocksAsync();
                Console.WriteLine($"Найдено товаров в стоке Ozon: {ozonStocks.Products.Count}");

                var fboSales = await ozonApiService.GetSalesReportAsync(DateTime.UtcNow, "FBO", ozonStocks.Products, ozonStocks.SkuToOfferId);
                var fbsSales = await ozonApiService.GetSalesReportAsync(DateTime.UtcNow, "FBS", ozonStocks.Products, ozonStocks.SkuToOfferId);

                var allOzonSalesDict = new Dictionary<string, ProductSalesReport>(StringComparer.OrdinalIgnoreCase);

                Action<List<ProductSalesReport>> mergeOzonData = (reports) => {
                    foreach (var r in reports)
                    {
                        if (!allOzonSalesDict.TryGetValue(r.OfferId, out var existing))
                        {
                            allOzonSalesDict[r.OfferId] = r;
                        }
                        else
                        {
                            existing.Total7 += r.Total7;
                            existing.AvgPerDay = existing.Total7 / 7m;
                            existing.Revenue7 += r.Revenue7;
                        }
                    }
                };

                mergeOzonData(fboSales);
                mergeOzonData(fbsSales);

                var finalOzonData = allOzonSalesDict.Values.ToList();
                Console.WriteLine($"Скачано отчетов о продажах Ozon (FBO+FBS): {finalOzonData.Count}");

                // РАСКОММЕНТИРОВАНО: Запись в таблицу Ozon
                Console.WriteLine("Обновление Google Таблицы (Ozon)...");
                await googleSheetsService.WriteOzonDataAsync(spreadsheetId, sheetName, finalOzonData);


                // ==========================================
                // 5. YANDEX MARKET
                // ==========================================
                Console.WriteLine("\n[3/3] Загрузка данных Yandex Market...");
                var masterYandexList = new List<YandexReportItem>();

                var campaigns = await yandexApiService.GetCampaignsAsync();
                Console.WriteLine($"Найдено магазинов Яндекса: {campaigns.Count}");

                foreach (var campaign in campaigns)
                {
                    string campId = campaign.Id.ToString();
                    await yandexApiService.GetStocksAsync(campId, masterYandexList);
                    await yandexApiService.Get7DaySalesAsync(campId, masterYandexList);
                }

                Console.WriteLine($"Всего уникальных товаров Yandex собрано: {masterYandexList.Count}");

                // РАСКОММЕНТИРОВАНО: Запись в таблицу Yandex
                Console.WriteLine("Обновление Google Таблицы (Yandex)...");
                await googleSheetsService.WriteYandexDataAsync(spreadsheetId, sheetName, masterYandexList);

                // ==========================================
                // ГОТОВО!
                // ==========================================
                Console.WriteLine("\n=== ВСЕ ДАННЫЕ УСПЕШНО СОБРАНЫ И ВЫГРУЖЕНЫ В GOOGLE ТАБЛИЦУ! ===");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n[КРИТИЧЕСКАЯ ОШИБКА]: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
            }
        }
    }
}