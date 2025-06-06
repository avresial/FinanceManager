using FinanceManager.Domain.Entities;
using System.Net.Http.Json;

namespace FinanceManager.Components.Services;

internal class DuplicateEntryResolverService(HttpClient httpClient)
{
    private readonly HttpClient _httpClient = httpClient;

    public async Task ScanAsync(int accountId)
    {
        var response = await _httpClient.PostAsync($"{_httpClient.BaseAddress}/Scan?accountId={accountId}", null);
        response.EnsureSuccessStatusCode();
    }

    public async Task RemoveDuplicateAsync(int duplicateId)
    {
        var response = await _httpClient.DeleteAsync($"{_httpClient.BaseAddress}/RemoveDuplicate?duplicateId={duplicateId}");
        response.EnsureSuccessStatusCode();
    }

    public async Task<int> GetDuplicatesCountAsync(int accountId)
    {
        return await _httpClient.GetFromJsonAsync<int>($"{_httpClient.BaseAddress}/GetDuplicatesCount?accountId={accountId}");
    }

    public async Task<DuplicateEntry?> GetDuplicateAsync(int accountId, int entryIndex)
    {
        return await _httpClient.GetFromJsonAsync<DuplicateEntry>($"{_httpClient.BaseAddress}/GetDuplicate?accountId={accountId}&entryIndex={entryIndex}");
    }

    public async Task<IEnumerable<DuplicateEntry>?> GetDuplicatesAsync(int accountId, int index, int count)
    {
        return await _httpClient.GetFromJsonAsync<IEnumerable<DuplicateEntry>>($"{_httpClient.BaseAddress}/GetDuplicates?accountId={accountId}&index={index}&count={count}");
    }
}
