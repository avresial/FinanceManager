using FinanceManager.Domain.FinancialAccounts.Currencies.Dtos;
using System.Net.Http.Json;

namespace FinanceManager.Components.Features.FinancialAccounts.HttpClients;

public class CurrencyAccountImportHttpClient(HttpClient httpClient)
{
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
}