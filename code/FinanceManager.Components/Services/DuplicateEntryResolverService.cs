using FinanceManager.Domain.Entities;
using System.Net.Http.Json;

namespace FinanceManager.Components.Services;

public class DuplicateEntryResolverService(HttpClient httpClient)
{
    public async Task Scan(int accountId)
    {
        var response = await httpClient.PostAsync($"{httpClient.BaseAddress}api/DuplicateEntryResolver/Scan?accountId={accountId}", null);
        response.EnsureSuccessStatusCode();
    }

    public async Task Resolve(int accountId, int duplicateId, int entryIdToBeRemained)
    {
        var response = await httpClient.PostAsync($"{httpClient.BaseAddress}api/DuplicateEntryResolver/Resolve?accountId={accountId}&duplicateId={duplicateId}&entryIdToBeRemained={entryIdToBeRemained}", null);
        response.EnsureSuccessStatusCode();
    }

    public Task<int> GetDuplicatesCount(int accountId)
    {
        return httpClient.GetFromJsonAsync<int>($"{httpClient.BaseAddress}api/DuplicateEntryResolver/GetDuplicatesCount?accountId={accountId}");
    }

    public Task<DuplicateEntry?> GetDuplicate(int accountId, int entryIndex)
    {
        return httpClient.GetFromJsonAsync<DuplicateEntry>($"{httpClient.BaseAddress}api/DuplicateEntryResolver/GetDuplicate?accountId={accountId}&entryIndex={entryIndex}");
    }

    public Task<IEnumerable<DuplicateEntry>?> GetDuplicates(int accountId, int index, int count)
    {
        return httpClient.GetFromJsonAsync<IEnumerable<DuplicateEntry>>($"{httpClient.BaseAddress}api/DuplicateEntryResolver/GetDuplicates?accountId={accountId}&index={index}&count={count}");
    }
}
