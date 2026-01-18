using FinanceManager.Domain.Dtos;
using FinanceManager.Domain.Entities.Imports;
using System.Net.Http.Json;

namespace FinanceManager.Components.HttpClients;

public class CurrencyAccountImportHttpClient(HttpClient httpClient)
{
    public async Task<ImportResult?> ImportCurrencyEntriesAsync(CurrencyDataImportDto importDto)
    {
        var response = await httpClient.PostAsJsonAsync($"{httpClient.BaseAddress}api/BankAccountImport/ImportBankEntries", importDto);
        if (!response.IsSuccessStatusCode) throw new Exception(await response.Content.ReadAsStringAsync());
        return await response.Content.ReadFromJsonAsync<ImportResult?>();
    }

    public async Task<bool> ResolveImportConflictsAsync(IEnumerable<ResolvedImportConflict> resolvedConflicts)
    {
        var response = await httpClient.PostAsJsonAsync($"{httpClient.BaseAddress}api/BankAccountImport/ResolveImportConflicts", resolvedConflicts);
        if (!response.IsSuccessStatusCode) throw new Exception(await response.Content.ReadAsStringAsync());
        return true;
    }
}