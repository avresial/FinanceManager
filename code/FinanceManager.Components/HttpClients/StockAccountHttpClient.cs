using FinanceManager.Application.Commands.Account;
using FinanceManager.Domain.Entities.Stocks;
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

    public async Task<StockAccount?> GetAccountAsync(int accountId)
    {
        var result = await httpClient.GetFromJsonAsync<StockAccountDto>($"{httpClient.BaseAddress}api/StockAccount/{accountId}");
        if (result is null) return null;

        return new StockAccount(result.UserId, result.AccountId, result.Name, []);
    }

    public async Task<StockAccount?> GetAccountWithEntriesAsync(int accountId, DateTime startDate, DateTime endDate)
    {
        var result = await httpClient.GetFromJsonAsync<StockAccountDto>($"{httpClient.BaseAddress}api/StockAccount/{accountId}&{startDate:O}&{endDate:O}");
        if (result is null) return null;

        Dictionary<string, StockAccountEntry> nextOlder = result.NextOlderEntries is null ? [] :
            result.NextOlderEntries.ToDictionary(x => x.Key, x => x.Value.ToStockAccountEntry());

        Dictionary<string, StockAccountEntry> nextYounger = result.NextYoungerEntries is null ? [] :
            result.NextYoungerEntries.ToDictionary(x => x.Key, x => x.Value.ToStockAccountEntry());

        return new(result.UserId, result.AccountId, result.Name, result.Entries
            .Select(x => new StockAccountEntry(x.AccountId, x.EntryId, x.PostingDate, x.Value, x.ValueChange, x.Ticker, x.InvestmentType)),
            nextOlder, nextYounger);
    }

    public async Task<int?> AddAccountAsync(AddAccount addAccount)
    {
        var response = await httpClient.PostAsJsonAsync($"{httpClient.BaseAddress}api/StockAccount/Add", addAccount);
        if (response.IsSuccessStatusCode) return await response.Content.ReadFromJsonAsync<int?>();
        throw new Exception(await response.Content.ReadAsStringAsync());
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
}