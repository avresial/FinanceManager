// Test-only copy of the Components client that called the legacy account API. The Components client was removed with the
// account screens (#1142); the server routes stay until #1139 deletes them, so their tests keep this client.
using FinanceManager.Domain.FinancialAccounts.Currencies.Dtos;
using FinanceManager.Domain.FinancialAccounts.Shared.Dtos;
using FinanceManager.Domain.FinancialAccounts.Shared.Imports;
using System.Net.Http.Json;

namespace FinanceManager.Tests.Integration.Support.LegacyAccountClients;

public class CurrencyAccountImportClient(HttpClient httpClient)
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

    public async Task<bool> ResolveImportConflictsAsync(IEnumerable<ResolvedImportConflict> resolvedConflicts)
    {
        var response = await httpClient.PostAsJsonAsync($"{httpClient.BaseAddress}api/CurrencyAccountImport/ResolveImportConflicts", resolvedConflicts);
        if (!response.IsSuccessStatusCode) throw new Exception(await response.Content.ReadAsStringAsync());
        return true;
    }
}