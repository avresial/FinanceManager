using FinanceManager.Application.Commands.Account;
using FinanceManager.Domain.Entities.Accounts;
using FinanceManager.Domain.Entities.Accounts.Entries;
using FinanceManager.Domain.ValueObjects;
using FinanceManager.Infrastructure.Dtos;
using System.Net.Http.Json;

namespace FinanceManager.Components.Services;

public class BankAccountService(HttpClient httpClient)
{
    private readonly HttpClient _httpClient = httpClient;

    public async Task<IEnumerable<AvailableAccount>> GetAvailableAccountsAsync()
    {
        var response = await _httpClient.GetAsync($"{_httpClient.BaseAddress}api/BankAccount");
        var result = await response.Content.ReadFromJsonAsync<IEnumerable<AvailableAccount>>();
        if (result is null) return [];
        return result;
    }

    public async Task<BankAccount?> GetAccountAsync(int accountId)
    {
        return await _httpClient.GetFromJsonAsync<BankAccount>($"{_httpClient.BaseAddress}api/BankAccount/{accountId}");
    }

    public async Task<BankAccount?> GetAccountWithEntriesAsync(int accountId, DateTime startDate, DateTime endDate)
    {
        var result = await _httpClient.GetFromJsonAsync<BankAccountDto>($"{_httpClient.BaseAddress}api/BankAccount/{accountId}&{startDate:O}&{endDate:O}");

        if (result is null) return null;
        return new BankAccount(result.UserId, result.AccountId, result.Name, result.Entries.Select(x => new BankAccountEntry(x.AccountId, x.EntryId, x.PostingDate, x.Value, x.ValueChange) { ExpenseType = x.ExpenseType, Description = x.Description })
            , result.AccountType, result.OlderThenLoadedEntry, result.YoungerThenLoadedEntry);
    }
    public async Task<DateTime?> GetOldestEntryDate(int accountId)
    {
        var response = await _httpClient.GetAsync($"{_httpClient.BaseAddress}api/BankAccount/GetOldestEntryDate/{accountId}");
        if (response.StatusCode == System.Net.HttpStatusCode.NoContent) return null;
        var result = await response.Content.ReadFromJsonAsync<DateTime?>();
        if (result is null) return null;
        return result;
    }
    public async Task<DateTime?> GetYoungestEntryDate(int accountId)
    {
        var response = await _httpClient.GetAsync($"{_httpClient.BaseAddress}api/BankAccount/GetYoungestEntryDate/{accountId}");
        if (response.StatusCode == System.Net.HttpStatusCode.NoContent) return null;
        var result = await response.Content.ReadFromJsonAsync<DateTime?>();
        if (result is null) return null;
        return result;
    }
    public async Task<int?> AddAccountAsync(AddAccount addAccount)
    {
        var response = await _httpClient.PostAsJsonAsync($"{_httpClient.BaseAddress}api/BankAccount/Add", addAccount);
        return await response.Content.ReadFromJsonAsync<int?>();
    }

    public async Task<bool> AddEntryAsync(AddBankAccountEntry addEntry)
    {
        var response = await _httpClient.PostAsJsonAsync($"{_httpClient.BaseAddress}api/BankAccount/AddEntry", addEntry);

        if (response.IsSuccessStatusCode) return true;

        throw new Exception(await response.Content.ReadAsStringAsync()); // TODO make custom exception
    }

    public async Task<bool> UpdateAccountAsync(UpdateAccount updateAccount)
    {
        var response = await _httpClient.PutAsJsonAsync($"{_httpClient.BaseAddress}api/BankAccount/Update", updateAccount);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> DeleteAccountAsync(DeleteAccount deleteAccount)
    {
        return await _httpClient.DeleteFromJsonAsync<bool>($"{_httpClient.BaseAddress}api/BankAccount/Delete/{deleteAccount.accountId}");
    }

    public async Task<bool> DeleteEntryAsync(int accountId, int entryId)
    {
        var response = await _httpClient.DeleteAsync($"{_httpClient.BaseAddress}api/BankAccount/DeleteEntry/{accountId}/{entryId}");
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> UpdateEntryAsync(BankAccountEntry entry)
    {
        var response = await _httpClient.PutAsJsonAsync($"{_httpClient.BaseAddress}api/BankAccount/UpdateEntry", entry);
        return response.IsSuccessStatusCode;
    }
}
