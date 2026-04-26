using FinanceManager.Application.Commands.Account;
using FinanceManager.Domain.Commands.Account;
using FinanceManager.Domain.Entities.Bonds;
using FinanceManager.Domain.ValueObjects;
using FinanceManager.Infrastructure.Dtos;
using System.Net.Http.Json;

namespace FinanceManager.Components.HttpClients;

public class BondAccountHttpClient(HttpClient httpClient)
{
    public async Task<IEnumerable<AvailableAccount>> GetAvailableAccountsAsync()
    {
        var response = await httpClient.GetAsync($"{httpClient.BaseAddress}api/BondAccount");
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound) return [];
        var result = await response.Content.ReadFromJsonAsync<IEnumerable<AvailableAccount>>();
        return result ?? [];
    }

    public async Task<BondAccount?> GetAccountAsync(int accountId)
    {
        var result = await httpClient.GetFromJsonAsync<BondAccountDto>($"{httpClient.BaseAddress}api/BondAccount/{accountId}");
        if (result is null) return null;

        return new BondAccount(result.UserId, result.AccountId, result.Name, [], result.AccountLabel);
    }

    public async Task<BondAccount?> GetAccountWithEntriesAsync(int accountId, DateTime startDate, DateTime endDate, int minimumEntryCount = 0)
    {
        var minimumEntryCountQuery = minimumEntryCount > 0 ? $"?minimumEntryCount={minimumEntryCount}" : string.Empty;
        var result = await httpClient.GetFromJsonAsync<BondAccountDto>($"{httpClient.BaseAddress}api/BondAccount/{accountId}/{startDate:O}/{endDate:O}{minimumEntryCountQuery}");
        return MapAccount(result);
    }

    public async Task<BondAccount?> GetAccountWithEntriesAsync(int accountId, DateTime date, int count, bool olderThenDate = true)
    {
        var encodedDate = Uri.EscapeDataString(date.ToString("O"));
        var result = await httpClient.GetFromJsonAsync<BondAccountDto>($"{httpClient.BaseAddress}api/BondAccount/{accountId}/entries?date={encodedDate}&count={count}&olderThenDate={olderThenDate.ToString().ToLowerInvariant()}");
        return MapAccount(result);
    }

    private static BondAccount? MapAccount(BondAccountDto? result)
    {
        if (result is null) return null;

        Dictionary<int, BondAccountEntry> nextOlder = result.NextOlderEntries is null ? [] :
            result.NextOlderEntries.ToDictionary(x => x.Key, x => x.Value.ToBondAccountEntry());

        Dictionary<int, BondAccountEntry> nextYounger = result.NextYoungerEntries is null ? [] :
            result.NextYoungerEntries.ToDictionary(x => x.Key, x => x.Value.ToBondAccountEntry());

        var entries = result.Entries
            .Select(x => new BondAccountEntry(x.AccountId, x.EntryId, x.PostingDate, x.Value, x.ValueChange, x.BondDetailsId) { Labels = x.Labels })
            .OrderByDescending(x => x.PostingDate)
            .ThenByDescending(x => x.EntryId);

        return new(result.UserId, result.AccountId, result.Name, entries,
            result.AccountLabel, nextOlder, nextYounger);
    }

    public async Task<int?> AddAccountAsync(AddAccount addAccount)
    {
        var response = await httpClient.PostAsJsonAsync($"{httpClient.BaseAddress}api/BondAccount", addAccount);
        if (response.IsSuccessStatusCode) return await response.Content.ReadFromJsonAsync<int?>();
        throw new Exception(await response.Content.ReadAsStringAsync());
    }

    public async Task<bool> UpdateAccountAsync(UpdateAccount updateAccount)
    {
        var response = await httpClient.PutAsJsonAsync($"{httpClient.BaseAddress}api/BondAccount", updateAccount);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> DeleteAccountAsync(int accountId)
    {
        var response = await httpClient.DeleteAsync($"{httpClient.BaseAddress}api/BondAccount/{accountId}");
        return response.IsSuccessStatusCode;
    }
}
