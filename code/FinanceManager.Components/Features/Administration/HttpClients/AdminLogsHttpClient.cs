using FinanceManager.Domain.Administration.Logging;
using FinanceManager.Domain.FinancialAccounts.Shared.Dtos;
using System.Net.Http.Json;

namespace FinanceManager.Components.Features.Administration.HttpClients;

// Other typed clients in this project swallow exceptions and return empty
// collections, but the log viewer specifically needs to surface API/network
// failures: rendering "no warnings or errors" when the API is down would hide
// the very kind of incident this feature exists to expose. The card and page
// both wrap these calls in try/catch and set _loadError.
public class AdminLogsHttpClient(HttpClient httpClient)
{
    public async Task<List<LogEntryDto>> GetLatest(int count = 5)
    {
        var result = await httpClient.GetFromJsonAsync<List<LogEntryDto>>(
            $"{httpClient.BaseAddress}api/admin/logs/latest?count={count}");
        return result ?? [];
    }

    public async Task<PagedLogEntriesDto> GetPaged(int skip, int take, string? level = null)
    {
        var url = $"{httpClient.BaseAddress}api/admin/logs?skip={skip}&take={take}";
        if (!string.IsNullOrWhiteSpace(level))
            url += $"&level={Uri.EscapeDataString(level)}";

        var result = await httpClient.GetFromJsonAsync<PagedLogEntriesDto>(url);
        return result ?? new PagedLogEntriesDto([], 0);
    }
}