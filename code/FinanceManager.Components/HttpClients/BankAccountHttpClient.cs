using FinanceManager.Application.Commands.Account;
using FinanceManager.Domain.Entities.Cash;
using FinanceManager.Domain.ValueObjects;
using FinanceManager.Infrastructure.Dtos;
using System.Net.Http.Json;

namespace FinanceManager.Components.HttpClients;

public class BankAccountHttpClient(HttpClient httpClient)
{
    public async Task<IEnumerable<AvailableAccount>> GetAvailableAccountsAsync()
    {
        var response = await httpClient.GetAsync($"{httpClient.BaseAddress}api/BankAccount");
        var result = await response.Content.ReadFromJsonAsync<IEnumerable<AvailableAccount>>();
        return result ?? [];
    }

    public Task<BankAccount?> GetAccountAsync(int accountId) =>
        httpClient.GetFromJsonAsync<BankAccount>($"{httpClient.BaseAddress}api/BankAccount/{accountId}");

    public async Task<BankAccount?> GetAccountWithEntriesAsync(int accountId, DateTime startDate, DateTime endDate)
    {
        var result = await httpClient.GetFromJsonAsync<BankAccountDto>($"{httpClient.BaseAddress}api/BankAccount/{accountId}&{startDate:O}&{endDate:O}");
        if (result is null) return null;

        BankAccountEntry? nextOlderEntry = result.NextOlderEntry is null ? null : new(result.NextOlderEntry.AccountId, result.NextOlderEntry.EntryId,
            result.NextOlderEntry.PostingDate, result.NextOlderEntry.Value, result.NextOlderEntry.ValueChange);

        BankAccountEntry? nextYoungerEntry = result.NextYoungerEntry is null ? null : new(result.NextYoungerEntry.AccountId, result.NextYoungerEntry.EntryId,
            result.NextYoungerEntry.PostingDate, result.NextYoungerEntry.Value, result.NextYoungerEntry.ValueChange);

        return new(result.UserId, result.AccountId, result.Name, result.Entries.Select(x => new BankAccountEntry(x.AccountId, x.EntryId, x.PostingDate, x.Value, x.ValueChange)
        {
            Description = x.Description,
            Labels = x.Labels
        }), result.AccountLabel, nextOlderEntry, nextYoungerEntry);
    }

    public async Task<int?> AddAccountAsync(AddAccount addAccount)
    {
        var response = await httpClient.PostAsJsonAsync($"{httpClient.BaseAddress}api/BankAccount", addAccount);
        if (response.IsSuccessStatusCode) return await response.Content.ReadFromJsonAsync<int?>();
        throw new Exception(await response.Content.ReadAsStringAsync());
    }

    public async Task<bool> UpdateAccountAsync(UpdateAccount updateAccount)
    {
        var response = await httpClient.PutAsJsonAsync($"{httpClient.BaseAddress}api/BankAccount", updateAccount);
        return response.IsSuccessStatusCode;
    }

    public Task<bool> DeleteAccountAsync(int accountId) =>
         httpClient.DeleteFromJsonAsync<bool>($"{httpClient.BaseAddress}api/BankAccount/{accountId}");
}