using FinanceManager.Domain.Dtos;
using System.Net.Http.Json;

namespace FinanceManager.Components.HttpClients;

public class AdminLogsHttpClient(HttpClient httpClient)
{
    public async Task<List<LogEntryDto>> GetLatest(int count = 5)
    {
        try
        {
            var result = await httpClient.GetFromJsonAsync<List<LogEntryDto>>(
                $"{httpClient.BaseAddress}api/admin/logs/latest?count={count}");
            return result ?? [];
        }
        catch
        {
            return [];
        }
    }

    public async Task<PagedLogEntriesDto> GetPaged(int skip, int take, string? level = null)
    {
        try
        {
            var url = $"{httpClient.BaseAddress}api/admin/logs?skip={skip}&take={take}";
            if (!string.IsNullOrWhiteSpace(level))
                url += $"&level={Uri.EscapeDataString(level)}";

            var result = await httpClient.GetFromJsonAsync<PagedLogEntriesDto>(url);
            return result ?? new PagedLogEntriesDto([], 0);
        }
        catch
        {
            return new PagedLogEntriesDto([], 0);
        }
    }
}