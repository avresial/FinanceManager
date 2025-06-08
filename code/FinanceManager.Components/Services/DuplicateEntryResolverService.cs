using FinanceManager.Domain.Entities;
using System.Net.Http.Json;

namespace FinanceManager.Components.Services;

public class DuplicateEntryResolverService(HttpClient httpClient)
{
    private readonly HttpClient _httpClient = httpClient;

    public async Task Scan(int accountId)
    {
        var response = await _httpClient.PostAsync($"{_httpClient.BaseAddress}api/DuplicateEntryResolver/Scan?accountId={accountId}", null);
        response.EnsureSuccessStatusCode();
    }

    public async Task Resolve(int accountId, int duplicateId, int entryIdToBeRemained)
    {
        var response = await _httpClient.PostAsync($"{_httpClient.BaseAddress}api/DuplicateEntryResolver/Resolve?accountId={accountId}&duplicateId={duplicateId}&entryIdToBeRemained={entryIdToBeRemained}", null);
        response.EnsureSuccessStatusCode();
    }

    public async Task<int> GetDuplicatesCount(int accountId)
    {
        return await _httpClient.GetFromJsonAsync<int>($"{_httpClient.BaseAddress}api/DuplicateEntryResolver/GetDuplicatesCount?accountId={accountId}");
    }

    public async Task<DuplicateEntry?> GetDuplicate(int accountId, int entryIndex)
    {
        return await _httpClient.GetFromJsonAsync<DuplicateEntry>($"{_httpClient.BaseAddress}api/DuplicateEntryResolver/GetDuplicate?accountId={accountId}&entryIndex={entryIndex}");
    }

    public async Task<IEnumerable<DuplicateEntry>?> GetDuplicates(int accountId, int index, int count)
    {
        return await _httpClient.GetFromJsonAsync<IEnumerable<DuplicateEntry>>($"{_httpClient.BaseAddress}api/DuplicateEntryResolver/GetDuplicates?accountId={accountId}&index={index}&count={count}");
    }
}
