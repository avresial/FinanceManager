using FinanceManager.Application.Commands.Account;
using FinanceManager.Domain.Dtos;
using FinanceManager.Domain.Entities.FinancialAccounts.Currencies;
using FinanceManager.Domain.ValueObjects;
using System.Net.Http.Json;

namespace FinanceManager.Components.HttpClients;

public class CurrencyAccountHttpClient(HttpClient httpClient)
{
    public async Task<IEnumerable<AvailableAccount>> GetAvailableAccountsAsync()
    {
        var response = await httpClient.GetAsync($"{httpClient.BaseAddress}api/CurrencyAccount");
        var result = await response.Content.ReadFromJsonAsync<IEnumerable<AvailableAccount>>();
        return result ?? [];
    }

    public Task<CurrencyAccount?> GetAccountAsync(int accountId) =>
        httpClient.GetFromJsonAsync<CurrencyAccount>($"{httpClient.BaseAddress}api/CurrencyAccount/{accountId}");

    public async Task<CurrencyAccount?> GetAccountWithEntriesAsync(int accountId, DateTime startDate, DateTime endDate)
    {
        var result = await httpClient.GetFromJsonAsync<CurrencyAccountDto>($"{httpClient.BaseAddress}api/CurrencyAccount/{accountId}&{startDate:O}&{endDate:O}");
        if (result is null) return null;

        CurrencyAccountEntry? nextOlderEntry = result.NextOlderEntry is null ? null : new(result.NextOlderEntry.AccountId, result.NextOlderEntry.EntryId,
            result.NextOlderEntry.PostingDate, result.NextOlderEntry.Value, result.NextOlderEntry.ValueChange);

        CurrencyAccountEntry? nextYoungerEntry = result.NextYoungerEntry is null ? null : new(result.NextYoungerEntry.AccountId, result.NextYoungerEntry.EntryId,
            result.NextYoungerEntry.PostingDate, result.NextYoungerEntry.Value, result.NextYoungerEntry.ValueChange);

        return new(result.UserId, result.AccountId, result.Name, result.Entries.Select(x => new CurrencyAccountEntry(x.AccountId, x.EntryId, x.PostingDate, x.Value, x.ValueChange)
        {
            Description = x.Description,
            Labels = x.Labels
        }), result.AccountLabel, nextOlderEntry, nextYoungerEntry);
    }

    public async Task<int?> AddAccountAsync(AddAccount addAccount)
    {
        var response = await httpClient.PostAsJsonAsync($"{httpClient.BaseAddress}api/CurrencyAccount", addAccount);
        if (response.IsSuccessStatusCode) return await response.Content.ReadFromJsonAsync<int?>();
        throw new Exception(await response.Content.ReadAsStringAsync());
    }

    public async Task<bool> UpdateAccountAsync(UpdateAccount updateAccount)
    {
        var response = await httpClient.PutAsJsonAsync($"{httpClient.BaseAddress}api/CurrencyAccount", updateAccount);
        return response.IsSuccessStatusCode;
    }

    public Task<bool> DeleteAccountAsync(int accountId) =>
         httpClient.DeleteFromJsonAsync<bool>($"{httpClient.BaseAddress}api/CurrencyAccount/{accountId}");
}