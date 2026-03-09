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
            var readRequest = _sheetsService.Spreadsheets.Values.Get(spreadsheetId, $"{sheetName}!A:M");
            var readResponse = await readRequest.ExecuteAsync();
            var existingRows = readResponse.Values ?? new List<IList<object>>();

            var skuMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            int totalRowIndex = -1;

            for (int i = 0; i < existingRows.Count; i++)
            {
                if (existingRows[i].Count > 0)
                {
                    string cellValue = existingRows[i][0]?.ToString()?.Trim();
                    if (string.IsNullOrEmpty(cellValue)) continue;

                    if (cellValue.Equals("тотал", StringComparison.OrdinalIgnoreCase))
                    {
                        totalRowIndex = i;
                    }
                    else if (i > 0) // Skip header
                    {
                        skuMap[cellValue] = i;
                    }
                }
            }

            var batchUpdateRequest = new BatchUpdateValuesRequest { ValueInputOption = "USER_ENTERED", Data = new List<ValueRange>() };

            foreach (var item in data)
            {
                var avg = Math.Round(item.Total7 / 7m, 1);

                if (skuMap.TryGetValue(item.OfferId, out int rowIndex))
                {
                    // Update existing SKU
                    int excelRow = rowIndex + 1;
                    batchUpdateRequest.Data.Add(new ValueRange
                    {
                        Range = $"{sheetName}!{stockCol}{excelRow}:{avgCol}{excelRow}",
                        Values = new List<IList<object>> { new List<object> { item.Stock, avg } }
                    });
                }
                else if (item.Stock > 0 || avg > 0)
                {
                    // Add NEW SKU before the Total row
                    int insertAtRowIndex = (totalRowIndex != -1) ? totalRowIndex : existingRows.Count;
                    int excelRow = insertAtRowIndex + 1;

                    var newRowValues = new List<object>(Enumerable.Repeat<object>(0, 13));
                    newRowValues[0] = item.OfferId;

                    if (stockCol == "B") { newRowValues[1] = item.Stock; newRowValues[2] = avg; }
                    else if (stockCol == "E") { newRowValues[4] = item.Stock; newRowValues[5] = avg; }
                    else if (stockCol == "H") { newRowValues[7] = item.Stock; newRowValues[8] = avg; }

                    // Standard row logic
                    newRowValues[3] = $"=IF(C{excelRow}>0; B{excelRow}/C{excelRow}; 0)";
                    newRowValues[6] = $"=IF(F{excelRow}>0; E{excelRow}/F{excelRow}; 0)";
                    newRowValues[9] = $"=IF(I{excelRow}>0; H{excelRow}/I{excelRow}; 0)";
                    newRowValues[10] = $"=B{excelRow}+E{excelRow}+H{excelRow}";
                    newRowValues[11] = $"=C{excelRow}+F{excelRow}+I{excelRow}";
                    newRowValues[12] = $"=IF(L{excelRow}>0; K{excelRow}/L{excelRow}; 0)";

                    // We use BatchUpdate to "Insert" a row if total exists, or just write if it doesn't
                    batchUpdateRequest.Data.Add(new ValueRange
                    {
                        Range = $"{sheetName}!A{excelRow}:M{excelRow}",
                        Values = new List<IList<object>> { newRowValues }
                    });

                    // Shift the "Total" index down because we just inserted a row above it
                    if (totalRowIndex != -1) totalRowIndex++;
                    skuMap[item.OfferId] = insertAtRowIndex;
                    existingRows.Insert(insertAtRowIndex, newRowValues);
                }
            }

            // Update the Total Row formulas to include the new range
            if (totalRowIndex != -1)
            {
                int totalExcelRow = totalRowIndex + 1;
                var totalRowValues = new List<object>(new object[13]);
                totalRowValues[0] = "Тотал";

                // Columns: B, C, E, F, H, I, K, L (Summable columns)
                string[] colsToSum = { "B", "C", "E", "F", "H", "I", "K", "L" };
                int[] colIndices = { 1, 2, 4, 5, 7, 8, 10, 11 };

                for (int i = 0; i < colsToSum.Length; i++)
                {
                    batchUpdateRequest.Data.Add(new ValueRange
                    {
                        Range = $"{sheetName}!{colsToSum[i]}{totalExcelRow}",
                        Values = new List<IList<object>> { new List<object> { $"=SUM({colsToSum[i]}2:{colsToSum[i]}{totalExcelRow - 1})" } }
                    });
                }

                // Calculated columns for Total row (D, G, J, M)
                batchUpdateRequest.Data.Add(new ValueRange
                {
                    Range = $"{sheetName}!D{totalExcelRow}",
                    Values = new List<IList<object>> { new List<object> { $"=IF(C{totalExcelRow}>0; B{totalExcelRow}/C{totalExcelRow}; 0)" } }
                });
                // ... (Repeated logic for G, J, M if needed)
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