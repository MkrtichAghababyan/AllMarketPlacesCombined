using AllMarketPlacesCombined.Models.OzonModels;
using AllMarketPlacesCombined.Models.WBModels; // ADDED: The Wildberries models namespace
using AllMarketPlacesCombined.Models.YandexModels;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.Sheets.v4;
using Google.Apis.Sheets.v4.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AllMarketPlacesCombined.Services.GoogleSheetService // Adjusted namespace
{
    public class GoogleSheetsService
    {
        private readonly SheetsService _sheetsService;

        public GoogleSheetsService(string credentialsFilePath)
        {
            var credential = GoogleCredential.FromFile(credentialsFilePath)
                .CreateScoped(SheetsService.Scope.Spreadsheets);

            _sheetsService = new SheetsService(new BaseClientService.Initializer()
            {
                HttpClientInitializer = credential,
                ApplicationName = "MarketplaceAnalyticsApp"
            });
        }

        // ==========================================
        // 1. WILDBERRIES (Columns B and C)
        // ==========================================
        // FIXED: Changed WbProduct to WbAnalyticsOfferSummary
        public async Task WriteWbDataAsync(string spreadsheetId, string sheetName, List<WbAnalyticsOfferSummary> wbData)
        {
            var readRequest = _sheetsService.Spreadsheets.Values.Get(spreadsheetId, $"{sheetName}!A1:A");
            var readResponse = await readRequest.ExecuteAsync();
            var existingRows = readResponse.Values;

            if (existingRows == null || existingRows.Count == 0) return;

            var wbDict = wbData.ToDictionary(x => x.OfferId, StringComparer.OrdinalIgnoreCase);
            var batchUpdateRequest = new BatchUpdateValuesRequest { ValueInputOption = "USER_ENTERED", Data = new List<ValueRange>() };

            for (int i = 0; i < existingRows.Count; i++)
            {
                int excelRowNumber = i + 1;
                if (existingRows[i].Count == 0) continue;

                var sheetOfferId = existingRows[i][0].ToString()?.Trim();
                if (sheetOfferId != null && sheetOfferId.Equals("артикул", StringComparison.OrdinalIgnoreCase)) continue;

                if (!string.IsNullOrEmpty(sheetOfferId))
                {
                    if (wbDict.TryGetValue(sheetOfferId, out var myWbItem))
                    {
                        // Safely calculate the average
                        var averageSales = Math.Round((decimal)myWbItem.Total7 / 7m, 1);
                        var valueRange = new ValueRange
                        {
                            Range = $"{sheetName}!B{excelRowNumber}:C{excelRowNumber}",
                            // FIXED: Changed myWbItem.Stock to myWbItem.TotalStock to match your WB model
                            Values = new List<IList<object>> { new List<object> { myWbItem.TotalStock, averageSales } }
                        };
                        batchUpdateRequest.Data.Add(valueRange);
                    }
                    else
                    {
                        var valueRange = new ValueRange
                        {
                            Range = $"{sheetName}!B{excelRowNumber}:C{excelRowNumber}",
                            Values = new List<IList<object>> { new List<object> { 0, 0 } }
                        };
                        batchUpdateRequest.Data.Add(valueRange);
                    }
                }
            }

            if (batchUpdateRequest.Data.Count > 0)
            {
                var batchUpdate = _sheetsService.Spreadsheets.Values.BatchUpdate(batchUpdateRequest, spreadsheetId);
                await batchUpdate.ExecuteAsync();
                Console.WriteLine($"Успешно выгружено строк WB: {batchUpdateRequest.Data.Count}");
            }
        }

        // ==========================================
        // 2. OZON (Columns E and F)
        // ==========================================
        public async Task WriteOzonDataAsync(string spreadsheetId, string sheetName, List<ProductSalesReport> ozonData)
        {
            var readRequest = _sheetsService.Spreadsheets.Values.Get(spreadsheetId, $"{sheetName}!A1:A");
            var readResponse = await readRequest.ExecuteAsync();
            var existingRows = readResponse.Values;

            if (existingRows == null || existingRows.Count == 0) return;

            var ozonDict = ozonData.ToDictionary(x => x.OfferId, StringComparer.OrdinalIgnoreCase);
            var batchUpdateRequest = new BatchUpdateValuesRequest { ValueInputOption = "USER_ENTERED", Data = new List<ValueRange>() };

            for (int i = 0; i < existingRows.Count; i++)
            {
                int excelRowNumber = i + 1;
                if (existingRows[i].Count == 0) continue;

                var sheetOfferId = existingRows[i][0].ToString()?.Trim();
                if (sheetOfferId != null && sheetOfferId.Equals("артикул", StringComparison.OrdinalIgnoreCase)) continue;

                if (!string.IsNullOrEmpty(sheetOfferId))
                {
                    if (ozonDict.TryGetValue(sheetOfferId, out var myOzonItem))
                    {
                        var averageSales = Math.Round((decimal)myOzonItem.Total7 / 7m, 1);
                        var valueRange = new ValueRange
                        {
                            Range = $"{sheetName}!E{excelRowNumber}:F{excelRowNumber}",
                            Values = new List<IList<object>> { new List<object> { myOzonItem.Stock, averageSales } }
                        };
                        batchUpdateRequest.Data.Add(valueRange);
                    }
                    else
                    {
                        var valueRange = new ValueRange
                        {
                            Range = $"{sheetName}!E{excelRowNumber}:F{excelRowNumber}",
                            Values = new List<IList<object>> { new List<object> { 0, 0 } }
                        };
                        batchUpdateRequest.Data.Add(valueRange);
                    }
                }
            }

            if (batchUpdateRequest.Data.Count > 0)
            {
                var batchUpdate = _sheetsService.Spreadsheets.Values.BatchUpdate(batchUpdateRequest, spreadsheetId);
                await batchUpdate.ExecuteAsync();
                Console.WriteLine($"Успешно выгружено строк Ozon: {batchUpdateRequest.Data.Count}");
            }
        }

        // ==========================================
        // 3. YANDEX MARKET (Columns H and I)
        // ==========================================
        public async Task WriteYandexDataAsync(string spreadsheetId, string sheetName, List<YandexReportItem> yandexData)
        {
            var readRequest = _sheetsService.Spreadsheets.Values.Get(spreadsheetId, $"{sheetName}!A1:A");
            var readResponse = await readRequest.ExecuteAsync();
            var existingRows = readResponse.Values;

            if (existingRows == null || existingRows.Count == 0) return;

            var yandexDict = yandexData.ToDictionary(x => x.OfferId, StringComparer.OrdinalIgnoreCase);
            var batchUpdateRequest = new BatchUpdateValuesRequest { ValueInputOption = "USER_ENTERED", Data = new List<ValueRange>() };

            for (int i = 0; i < existingRows.Count; i++)
            {
                int excelRowNumber = i + 1;
                if (existingRows[i].Count == 0) continue;

                var sheetOfferId = existingRows[i][0].ToString()?.Trim();
                if (sheetOfferId != null && sheetOfferId.Equals("артикул", StringComparison.OrdinalIgnoreCase)) continue;

                if (!string.IsNullOrEmpty(sheetOfferId))
                {
                    if (yandexDict.TryGetValue(sheetOfferId, out var myYandexItem))
                    {
                        var averageSales = Math.Round((decimal)myYandexItem.Total7 / 7m, 1);
                        var valueRange = new ValueRange
                        {
                            Range = $"{sheetName}!H{excelRowNumber}:I{excelRowNumber}",
                            Values = new List<IList<object>> { new List<object> { myYandexItem.Stock, averageSales } }
                        };
                        batchUpdateRequest.Data.Add(valueRange);
                    }
                    else
                    {
                        var valueRange = new ValueRange
                        {
                            Range = $"{sheetName}!H{excelRowNumber}:I{excelRowNumber}",
                            Values = new List<IList<object>> { new List<object> { 0, 0 } }
                        };
                        batchUpdateRequest.Data.Add(valueRange);
                    }
                }
            }

            if (batchUpdateRequest.Data.Count > 0)
            {
                var batchUpdate = _sheetsService.Spreadsheets.Values.BatchUpdate(batchUpdateRequest, spreadsheetId);
                await batchUpdate.ExecuteAsync();
                Console.WriteLine($"Успешно выгружено строк Yandex: {batchUpdateRequest.Data.Count}");
            }
        }
    }
}