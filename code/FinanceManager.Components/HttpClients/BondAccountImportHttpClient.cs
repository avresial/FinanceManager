using FinanceManager.Domain.Dtos;
using FinanceManager.Domain.Entities.Imports;
using System.Net.Http.Json;

namespace FinanceManager.Components.HttpClients;

public class BondAccountImportHttpClient(HttpClient httpClient)
{
    public async Task<BondImportResult?> ImportBondEntriesAsync(BondDataImportDto importDto)
    {
        var response = await httpClient.PostAsJsonAsync($"{httpClient.BaseAddress}api/BondAccountImport/ImportBondEntries", importDto);
        if (!response.IsSuccessStatusCode)
            throw new Exception(await response.Content.ReadAsStringAsync());

        return await response.Content.ReadFromJsonAsync<BondImportResult?>();
    }

    public async Task<bool> ResolveImportConflictsAsync(IEnumerable<ResolvedBondImportConflict> resolvedConflicts)
    {
        var response = await httpClient.PostAsJsonAsync($"{httpClient.BaseAddress}api/BondAccountImport/ResolveImportConflicts", resolvedConflicts);
        if (!response.IsSuccessStatusCode)
            throw new Exception(await response.Content.ReadAsStringAsync());

        return true;
    }
}