using FinanceManager.Application.Commands.Account;
using FinanceManager.Domain.Entities.Accounts;
using FinanceManager.Domain.Entities.Accounts.Entries;
using System.Net.Http.Json;

namespace FinanceManager.Components.Services;

public class BankAccountService
{
    private readonly HttpClient _httpClient;

    public BankAccountService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<IEnumerable<BankAccount>> GetAvailableAccountsAsync()
    {
        var result = await _httpClient.GetFromJsonAsync<IEnumerable<BankAccount>>("api/BankAccount");
        if (result is null) return [];
        return result;
    }

    public async Task<BankAccount?> GetAccountAsync(int accountId)
    {
        return await _httpClient.GetFromJsonAsync<BankAccount>($"api/BankAccount/{accountId}");
    }

    public async Task<BankAccount?> GetAccountWithEntriesAsync(int accountId, DateTime startDate, DateTime endDate)
    {
        return await _httpClient.GetFromJsonAsync<BankAccount>($"api/BankAccount/{accountId}&{startDate:O}&{endDate:O}");
    }

    public async Task<bool> AddAccountAsync(AddAccount addAccount)
    {
        var response = await _httpClient.PostAsJsonAsync("api/BankAccount/Add", addAccount);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> AddEntryAsync(AddBankAccountEntry addEntry)
    {
        var response = await _httpClient.PostAsJsonAsync("api/BankAccount/AddEntry", addEntry);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> UpdateAccountAsync(UpdateAccount updateAccount)
    {
        var response = await _httpClient.PutAsJsonAsync("api/BankAccount/Update", updateAccount);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> DeleteAccountAsync(DeleteAccount deleteAccount)
    {
        var response = await _httpClient.PostAsJsonAsync("api/BankAccount/Delete", deleteAccount);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> DeleteEntryAsync(int accountId, int entryId)
    {
        var response = await _httpClient.DeleteAsync($"api/BankAccount/DeleteEntry/{accountId}/{entryId}");
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> UpdateEntryAsync(BankAccountEntry entry)
    {
        var response = await _httpClient.PutAsJsonAsync("api/BankAccount/UpdateEntry", entry);
        return response.IsSuccessStatusCode;
    }
}
