using FinanceManager.Application.Commands.Account;
using FinanceManager.Domain.Entities.Accounts;
using FinanceManager.Domain.Entities.Accounts.Entries;
using FinanceManager.Domain.ValueObjects;
using FinanceManager.Infrastructure.Dtos;
using System.Net.Http.Json;

namespace FinanceManager.Components.HttpClients;

public class StockAccountHttpClient(HttpClient httpClient)
{
    public async Task<IEnumerable<AvailableAccount>> GetAvailableAccountsAsync()
    {
        var response = await httpClient.GetAsync($"{httpClient.BaseAddress}api/StockAccount");
        var result = await response.Content.ReadFromJsonAsync<IEnumerable<AvailableAccount>>();
        return result ?? [];
    }

    public Task<StockAccount?> GetAccountAsync(int accountId)
    {
        return httpClient.GetFromJsonAsync<StockAccount>($"{httpClient.BaseAddress}api/StockAccount/{accountId}");
    }

    public async Task<StockAccount?> GetAccountWithEntriesAsync(int accountId, DateTime startDate, DateTime endDate)
    {
        var result = await httpClient.GetFromJsonAsync<StockAccountDto>($"{httpClient.BaseAddress}api/StockAccount/{accountId}&{startDate:O}&{endDate:O}");
        if (result is null) return null;

        Dictionary<string, StockAccountEntry> nextOlder = result.NextOlderEntries is null ? new Dictionary<string, StockAccountEntry>() :
            result.NextOlderEntries.ToDictionary(x => x.Key, x => x.Value.ToStockAccountEntry());

        Dictionary<string, StockAccountEntry> nextYounger = result.NextYoungerEntries is null ? new Dictionary<string, StockAccountEntry>() :
            result.NextYoungerEntries.ToDictionary(x => x.Key, x => x.Value.ToStockAccountEntry());

        return new(result.UserId, result.AccountId, result.Name, result.Entries
            .Select(x => new StockAccountEntry(x.AccountId, x.EntryId, x.PostingDate, x.Value, x.ValueChange, x.Ticker, x.InvestmentType)),
            nextOlder, nextYounger);
    }

    public async Task<DateTime?> GetOldestEntryDate(int accountId)
    {
        var response = await httpClient.GetAsync($"{httpClient.BaseAddress}api/StockAccount/GetOldestEntryDate/{accountId}");
        if (response.StatusCode == System.Net.HttpStatusCode.NoContent) return null;
        return await response.Content.ReadFromJsonAsync<DateTime?>();
    }

    public async Task<DateTime?> GetYoungestEntryDate(int accountId)
    {
        var response = await httpClient.GetAsync($"{httpClient.BaseAddress}api/StockAccount/GetYoungestEntryDate/{accountId}");
        if (response.StatusCode == System.Net.HttpStatusCode.NoContent) return null;
        return await response.Content.ReadFromJsonAsync<DateTime?>();
    }

    public async Task<int?> AddAccountAsync(AddAccount addAccount)
    {
        var response = await httpClient.PostAsJsonAsync($"{httpClient.BaseAddress}api/StockAccount/Add", addAccount);
        if (response.IsSuccessStatusCode) return await response.Content.ReadFromJsonAsync<int?>();
        throw new Exception(await response.Content.ReadAsStringAsync());
    }

    public async Task<bool> AddEntryAsync(AddStockAccountEntry addEntry)
    {
        var response = await httpClient.PostAsJsonAsync($"{httpClient.BaseAddress}api/StockAccount/AddEntry", addEntry);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> UpdateAccountAsync(UpdateAccount updateAccount)
    {
        var response = await httpClient.PutAsJsonAsync($"{httpClient.BaseAddress}api/StockAccount/Update", updateAccount);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> DeleteAccountAsync(int accountId)
    {
        return await httpClient.DeleteFromJsonAsync<bool>($"{httpClient.BaseAddress}api/StockAccount/Delete/{accountId}");
    }

    public async Task<bool> DeleteEntryAsync(int accountId, int entryId)
    {
        var response = await httpClient.DeleteAsync($"{httpClient.BaseAddress}api/StockAccount/DeleteEntry/{accountId}/{entryId}");
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> UpdateEntryAsync(UpdateStockAccountEntry entry)
    {
        var response = await httpClient.PutAsJsonAsync($"{httpClient.BaseAddress}api/StockAccount/UpdateEntry", entry);
        return response.IsSuccessStatusCode;
    }
}