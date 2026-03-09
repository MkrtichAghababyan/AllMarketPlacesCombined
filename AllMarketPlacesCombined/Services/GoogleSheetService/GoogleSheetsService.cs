using AllMarketPlacesCombined.Models.OzonModels;
using AllMarketPlacesCombined.Models.WBModels;
using AllMarketPlacesCombined.Models.YandexModels;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.Sheets.v4;
using Google.Apis.Sheets.v4.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AllMarketPlacesCombined.Services.GoogleSheetService
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

        private async Task ProcessMarketplaceDataAsync(string spreadsheetId, string sheetName, string stockCol, string avgCol, List<MarketplaceDataDto> data)
        {
            // 1. Read the existing sheet to find current SKU positions
            var readRequest = _sheetsService.Spreadsheets.Values.Get(spreadsheetId, $"{sheetName}!A:M");
            var readResponse = await readRequest.ExecuteAsync();
            var existingRows = readResponse.Values ?? new List<IList<object>>();

            var skuMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            // Map existing SKUs to their row index
            for (int i = 1; i < existingRows.Count; i++) // Skip header row (0)
            {
                if (existingRows[i].Count > 0)
                {
                    string sku = existingRows[i][0]?.ToString()?.Trim();
                    if (!string.IsNullOrEmpty(sku))
                    {
                        skuMap[sku] = i;
                    }
                }
            }

            var batchUpdateRequest = new BatchUpdateValuesRequest { ValueInputOption = "USER_ENTERED", Data = new List<ValueRange>() };
            int nextNewRowIndex = existingRows.Count; // This points to the first empty row at the bottom

            foreach (var item in data)
            {
                var avg = Math.Round(item.Total7 / 7m, 1);

                if (skuMap.TryGetValue(item.OfferId, out int rowIndex))
                {
                    // --- UPDATE EXISTING SKU ---
                    int excelRow = rowIndex + 1;
                    batchUpdateRequest.Data.Add(new ValueRange
                    {
                        Range = $"{sheetName}!{stockCol}{excelRow}:{avgCol}{excelRow}",
                        Values = new List<IList<object>> { new List<object> { item.Stock, avg } }
                    });
                }
                else if (item.Stock > 0 || avg > 0)
                {
                    // --- ADD NEW SKU AT THE VERY END ---
                    int excelRow = nextNewRowIndex + 1;

                    var newRowValues = new List<object>(Enumerable.Repeat<object>("", 13));
                    newRowValues[0] = item.OfferId;

                    // Place data in the correct columns based on the marketplace
                    if (stockCol == "B") { newRowValues[1] = item.Stock; newRowValues[2] = avg; }
                    else if (stockCol == "E") { newRowValues[4] = item.Stock; newRowValues[5] = avg; }
                    else if (stockCol == "H") { newRowValues[7] = item.Stock; newRowValues[8] = avg; }

                    // Add Formulas for the new row
                    newRowValues[3] = $"=IF(C{excelRow}>0; B{excelRow}/C{excelRow}; 0)"; // WB Days
                    newRowValues[6] = $"=IF(F{excelRow}>0; E{excelRow}/F{excelRow}; 0)"; // Ozon Days
                    newRowValues[9] = $"=IF(I{excelRow}>0; H{excelRow}/I{excelRow}; 0)"; // Yandex Days
                    newRowValues[10] = $"=B{excelRow}+E{excelRow}+H{excelRow}";         // Total Stock
                    newRowValues[11] = $"=C{excelRow}+F{excelRow}+I{excelRow}";         // Total Avg
                    newRowValues[12] = $"=IF(L{excelRow}>0; K{excelRow}/L{excelRow}; 0)"; // Total Days

                    batchUpdateRequest.Data.Add(new ValueRange
                    {
                        Range = $"{sheetName}!A{excelRow}:M{excelRow}",
                        Values = new List<IList<object>> { newRowValues }
                    });

                    // Update our local tracking so the next new item goes below this one
                    skuMap[item.OfferId] = nextNewRowIndex;
                    nextNewRowIndex++;
                }
            }

            if (batchUpdateRequest.Data.Count > 0)
            {
                await _sheetsService.Spreadsheets.Values.BatchUpdate(batchUpdateRequest, spreadsheetId).ExecuteAsync();
            }
        }

        public async Task WriteWbDataAsync(string spreadsheetId, string sheetName, List<WbAnalyticsOfferSummary> wbData) =>
            await ProcessMarketplaceDataAsync(spreadsheetId, sheetName, "B", "C", wbData.Select(x => new MarketplaceDataDto { OfferId = x.OfferId, Stock = x.TotalStock, Total7 = x.Total7 }).ToList());

        public async Task WriteOzonDataAsync(string spreadsheetId, string sheetName, List<ProductSalesReport> ozonData) =>
            await ProcessMarketplaceDataAsync(spreadsheetId, sheetName, "E", "F", ozonData.Select(x => new MarketplaceDataDto { OfferId = x.OfferId, Stock = x.Stock, Total7 = x.Total7 }).ToList());

        public async Task WriteYandexDataAsync(string spreadsheetId, string sheetName, List<YandexReportItem> yandexData) =>
            await ProcessMarketplaceDataAsync(spreadsheetId, sheetName, "H", "I", yandexData.Select(x => new MarketplaceDataDto { OfferId = x.OfferId, Stock = x.Stock, Total7 = x.Total7 }).ToList());
    }

    internal class MarketplaceDataDto { public string OfferId { get; set; } public int Stock { get; set; } public int Total7 { get; set; } }
}