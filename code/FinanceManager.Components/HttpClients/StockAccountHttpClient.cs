using FinanceManager.Domain.Commands.Account;
using FinanceManager.Domain.FinancialAccounts.Stock.Entities;
using FinanceManager.Domain.ValueObjects;
using FinanceManager.Infrastructure.Dtos;
using System.Net.Http.Json;

namespace FinanceManager.Components.HttpClients;

public class StockAccountHttpClient(HttpClient httpClient)
{
    public async Task<List<AvailableAccount>> GetAvailableAccountsAsync()
    {
        try
        {
            var response = await httpClient.GetAsync($"{httpClient.BaseAddress}api/StockAccount");
            var result = await response.Content.ReadFromJsonAsync<List<AvailableAccount>>();
            return result ?? [];
        }
        catch (Exception)
        {
        }

        return [];
    }

    public async Task<StockAccount?> GetAccountAsync(int accountId)
    {
        var result = await httpClient.GetFromJsonAsync<StockAccountDto>($"{httpClient.BaseAddress}api/StockAccount/{accountId}");
        if (result is null) return null;

        return new StockAccount(result.UserId, result.AccountId, result.Name, []);
    }

    public async Task<StockAccount?> GetAccountWithEntriesAsync(int accountId, DateTime startDate, DateTime endDate, int minimumEntryCount = 0)
    {
        var minimumEntryCountQuery = minimumEntryCount > 0 ? $"?minimumEntryCount={minimumEntryCount}" : string.Empty;
        var result = await httpClient.GetFromJsonAsync<StockAccountDto>($"{httpClient.BaseAddress}api/StockAccount/{accountId}&{startDate:O}&{endDate:O}{minimumEntryCountQuery}");
        return MapAccount(result);
    }

    public Task<StockAccount?> GetInitialTransactionHistoryAsync(int accountId, DateTime startDate, DateTime endDate,
        int minimumEntryCount = 100) =>
        GetAccountWithEntriesAsync(accountId, startDate, endDate, minimumEntryCount);

    public async Task<StockAccount?> GetAccountWithEntriesAsync(int accountId, DateTime date, int count, bool olderThenDate = true)
    {
        var encodedDate = Uri.EscapeDataString(date.ToString("O"));
        var result = await httpClient.GetFromJsonAsync<StockAccountDto>($"{httpClient.BaseAddress}api/StockAccount/{accountId}/entries?date={encodedDate}&count={count}&olderThenDate={olderThenDate.ToString().ToLowerInvariant()}");
        return MapAccount(result);
    }

    private static StockAccount? MapAccount(StockAccountDto? result)
    {
        if (result is null) return null;

        Dictionary<string, StockAccountEntry> nextOlder = result.NextOlderEntries is null ? [] :
            result.NextOlderEntries.ToDictionary(x => x.Key, x => x.Value.ToStockAccountEntry());

        Dictionary<string, StockAccountEntry> nextYounger = result.NextYoungerEntries is null ? [] :
            result.NextYoungerEntries.ToDictionary(x => x.Key, x => x.Value.ToStockAccountEntry());

        var entries = result.Entries
            .Select(x => new StockAccountEntry(x.AccountId, x.EntryId, x.PostingDate, x.Value, x.ValueChange, x.Isin, x.InvestmentType) { Ticker = x.Ticker })
            .OrderByDescending(x => x.PostingDate)
            .ThenByDescending(x => x.EntryId);

        return new(result.UserId, result.AccountId, result.Name, entries, nextOlder, nextYounger);
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

    public Task<bool> DeleteAccountAsync(int accountId) =>
        httpClient.DeleteFromJsonAsync<bool>($"{httpClient.BaseAddress}api/StockAccount/Delete/{accountId}");
}