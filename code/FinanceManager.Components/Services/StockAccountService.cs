using FinanceManager.Application.Commands.Account;
using FinanceManager.Domain.Entities.Accounts;
using FinanceManager.Domain.Entities.Accounts.Entries;
using FinanceManager.Domain.ValueObjects;
using FinanceManager.Infrastructure.Dtos;
using System.Net.Http.Json;

namespace FinanceManager.Components.Services;

public class StockAccountService
{
    private readonly HttpClient _httpClient;

    public StockAccountService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<IEnumerable<AvailableAccount>> GetAvailableAccountsAsync()
    {
        var response = await _httpClient.GetAsync($"{_httpClient.BaseAddress}api/StockAccount");
        var result = await response.Content.ReadFromJsonAsync<IEnumerable<AvailableAccount>>();
        if (result is null) return [];
        return result;
    }

    public async Task<StockAccount?> GetAccountAsync(int accountId)
    {
        return await _httpClient.GetFromJsonAsync<StockAccount>($"{_httpClient.BaseAddress}api/StockAccount/{accountId}");
    }

    public async Task<StockAccount?> GetAccountWithEntriesAsync(int accountId, DateTime startDate, DateTime endDate)
    {
        var result = await _httpClient.GetFromJsonAsync<StockAccountDto>($"{_httpClient.BaseAddress}api/StockAccount/{accountId}&{startDate:O}&{endDate:O}");

        if (result is null) return null;
        return new StockAccount(result.UserId, result.AccountId, result.Name, result.Entries.Select(x => new StockAccountEntry(x.AccountId, x.EntryId, x.PostingDate, x.Value, x.ValueChange, x.Ticker, x.InvestmentType))
            , result.OlderThenLoadedEntry, result.YoungerThenLoadedEntry);
    }
    public async Task<DateTime?> GetOldestEntryDate(int accountId)
    {
        var response = await _httpClient.GetAsync($"{_httpClient.BaseAddress}api/StockAccount/GetOldestEntryDate/{accountId}");
        if (response.StatusCode == System.Net.HttpStatusCode.NoContent) return null;
        var result = await response.Content.ReadFromJsonAsync<DateTime?>();
        if (result is null) return null;
        return result;
    }
    public async Task<DateTime?> GetYoungestEntryDate(int accountId)
    {
        var response = await _httpClient.GetAsync($"{_httpClient.BaseAddress}api/StockAccount/GetYoungestEntryDate/{accountId}");
        if (response.StatusCode == System.Net.HttpStatusCode.NoContent) return null;
        var result = await response.Content.ReadFromJsonAsync<DateTime?>();
        if (result is null) return null;
        return result;
    }
    public async Task<int?> AddAccountAsync(AddAccount addAccount)
    {
        var response = await _httpClient.PostAsJsonAsync($"{_httpClient.BaseAddress}api/StockAccount/Add", addAccount);
        return await response.Content.ReadFromJsonAsync<int?>();
    }

    public async Task<bool> AddEntryAsync(AddStockAccountEntry addEntry)
    {
        var response = await _httpClient.PostAsJsonAsync($"{_httpClient.BaseAddress}api/StockAccount/AddEntry", addEntry);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> UpdateAccountAsync(UpdateAccount updateAccount)
    {
        var response = await _httpClient.PutAsJsonAsync($"{_httpClient.BaseAddress}api/StockAccount/Update", updateAccount);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> DeleteAccountAsync(DeleteAccount deleteAccount)
    {
        var response = await _httpClient.PostAsJsonAsync($"{_httpClient.BaseAddress}api/StockAccount/Delete", deleteAccount);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> DeleteEntryAsync(int accountId, int entryId)
    {
        var response = await _httpClient.DeleteAsync($"{_httpClient.BaseAddress}api/StockAccount/DeleteEntry/{accountId}/{entryId}");
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> UpdateEntryAsync(StockAccountEntry entry)
    {
        var response = await _httpClient.PutAsJsonAsync($"{_httpClient.BaseAddress}api/StockAccount/UpdateEntry", entry);
        return response.IsSuccessStatusCode;
    }
}
