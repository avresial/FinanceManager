using FinanceManager.Application.Commands.Account;
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

    public async Task<BondAccount?> GetAccountWithEntriesAsync(int accountId, DateTime startDate, DateTime endDate)
    {
        var result = await httpClient.GetFromJsonAsync<BondAccountDto>($"{httpClient.BaseAddress}api/BondAccount/{accountId}/{startDate:O}/{endDate:O}");
        if (result is null) return null;

        Dictionary<int, BondAccountEntry> nextOlder = result.NextOlderEntries is null ? [] :
            result.NextOlderEntries.ToDictionary(x => x.Key, x => x.Value.ToBondAccountEntry());

        Dictionary<int, BondAccountEntry> nextYounger = result.NextYoungerEntries is null ? [] :
            result.NextYoungerEntries.ToDictionary(x => x.Key, x => x.Value.ToBondAccountEntry());

        return new(result.UserId, result.AccountId, result.Name, result.Entries
            .Select(x => new BondAccountEntry(x.AccountId, x.EntryId, x.PostingDate, x.Value, x.ValueChange, x.BondDetailsId) { Labels = x.Labels }),
            result.AccountLabel, nextOlder, nextYounger);
    }

    public async Task<int?> AddAccountAsync(AddBondAccount addAccount)
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