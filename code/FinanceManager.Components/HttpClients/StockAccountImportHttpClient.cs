using FinanceManager.Domain.Dtos;
using FinanceManager.Domain.Entities.Imports;
using System.Net.Http.Json;

namespace FinanceManager.Components.HttpClients;

public class StockAccountImportHttpClient(HttpClient httpClient)
{
    public async Task<StockImportResult?> ImportStockEntriesAsync(StockDataImportDto importDto)
    {
        var response = await httpClient.PostAsJsonAsync($"{httpClient.BaseAddress}api/StockAccountImport/ImportStockEntries", importDto);
        if (!response.IsSuccessStatusCode) throw new Exception(await response.Content.ReadAsStringAsync());
        return await response.Content.ReadFromJsonAsync<StockImportResult?>();
    }

    public async Task<bool> ResolveImportConflictsAsync(IEnumerable<ResolvedStockImportConflict> resolvedConflicts)
    {
        var response = await httpClient.PostAsJsonAsync($"{httpClient.BaseAddress}api/StockAccountImport/ResolveImportConflicts", resolvedConflicts);
        if (!response.IsSuccessStatusCode) throw new Exception(await response.Content.ReadAsStringAsync());
        return true;
    }
}