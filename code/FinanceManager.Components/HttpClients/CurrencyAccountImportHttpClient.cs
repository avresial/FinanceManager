using FinanceManager.Domain.Dtos;
using FinanceManager.Domain.Entities.Imports;
using FinanceManager.Domain.FinancialAccounts.Currencies.Dtos;
using System.Net.Http.Json;

namespace FinanceManager.Components.HttpClients;

public class CurrencyAccountImportHttpClient(HttpClient httpClient)
{
    public async Task<CurrencyImportJobStartResponseDto?> StartAsyncCurrencyImportAsync(CurrencyDataImportDto importDto)
    {
        var response = await httpClient.PostAsJsonAsync($"{httpClient.BaseAddress}api/CurrencyAccountImport/StartAsyncImport", importDto);
        if (!response.IsSuccessStatusCode) throw new Exception(await response.Content.ReadAsStringAsync());
        return await response.Content.ReadFromJsonAsync<CurrencyImportJobStartResponseDto?>();
    }

    public async Task<CurrencyImportJobStatusDto?> GetCurrencyImportStatusAsync(Guid jobId)
    {
        var response = await httpClient.GetAsync($"{httpClient.BaseAddress}api/CurrencyAccountImport/ImportStatus/{jobId}");
        if (!response.IsSuccessStatusCode) throw new Exception(await response.Content.ReadAsStringAsync());
        return await response.Content.ReadFromJsonAsync<CurrencyImportJobStatusDto?>();
    }

    public async Task<CurrencyImportJobStatusDto?> ResolveAsyncCurrencyImportConflictsAsync(CurrencyImportResolveConflictsRequestDto request)
    {
        var response = await httpClient.PostAsJsonAsync($"{httpClient.BaseAddress}api/CurrencyAccountImport/ResolveAsyncImportConflicts", request);
        if (!response.IsSuccessStatusCode) throw new Exception(await response.Content.ReadAsStringAsync());
        return await response.Content.ReadFromJsonAsync<CurrencyImportJobStatusDto?>();
    }

    public async Task<ImportResult?> ImportCurrencyEntriesAsync(CurrencyDataImportDto importDto)
    {
        var response = await httpClient.PostAsJsonAsync($"{httpClient.BaseAddress}api/CurrencyAccountImport/ImportCurrencyEntries", importDto);
        if (!response.IsSuccessStatusCode) throw new Exception(await response.Content.ReadAsStringAsync());
        return await response.Content.ReadFromJsonAsync<ImportResult?>();
    }

    public async Task<bool> ResolveImportConflictsAsync(IEnumerable<ResolvedImportConflict> resolvedConflicts)
    {
        var response = await httpClient.PostAsJsonAsync($"{httpClient.BaseAddress}api/CurrencyAccountImport/ResolveImportConflicts", resolvedConflicts);
        if (!response.IsSuccessStatusCode) throw new Exception(await response.Content.ReadAsStringAsync());
        return true;
    }
}