// Test-only copy of the Components client that called the legacy account API. The Components client was removed with the
// account screens (#1142); the server routes stay until #1139 deletes them, so their tests keep this client.
using FinanceManager.Contracts.FinancialAccounts.Bond;
using FinanceManager.Domain.FinancialAccounts.Bond.Entities;
using FinanceManager.Domain.FinancialAccounts.Shared.Commands;
using FinanceManager.Domain.FinancialAccounts.Shared.Entities;
using FinanceManager.Domain.FinancialAccounts.Shared.ValueObjects;
using System.Net.Http.Json;

namespace FinanceManager.Tests.Integration.Support.LegacyAccountClients;

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

        return new BondAccount(result.UserId, result.AccountId, result.Name, [], (AccountLabel)result.AccountLabel);
    }

    public async Task<BondAccount?> GetAccountWithEntriesAsync(int accountId, DateTime startDate, DateTime endDate, int minimumEntryCount = 0)
    {
        var minimumEntryCountQuery = minimumEntryCount > 0 ? $"?minimumEntryCount={minimumEntryCount}" : string.Empty;
        var result = await httpClient.GetFromJsonAsync<BondAccountDto>($"{httpClient.BaseAddress}api/BondAccount/{accountId}/{startDate:O}/{endDate:O}{minimumEntryCountQuery}");
        return MapAccount(result);
    }

    public Task<BondAccount?> GetInitialTransactionHistoryAsync(int accountId, DateTime startDate, DateTime endDate,
        int minimumEntryCount = 100) =>
        GetAccountWithEntriesAsync(accountId, startDate, endDate, minimumEntryCount);

    private static BondAccount? MapAccount(BondAccountDto? result)
    {
        if (result is null) return null;

        Dictionary<int, BondAccountEntry> nextOlder = result.NextOlderEntries is null ? [] :
            result.NextOlderEntries.ToDictionary(x => x.Key, x => ToBondAccountEntry(x.Value));

        Dictionary<int, BondAccountEntry> nextYounger = result.NextYoungerEntries is null ? [] :
            result.NextYoungerEntries.ToDictionary(x => x.Key, x => ToBondAccountEntry(x.Value));

        var entries = result.Entries
            .Select(AccountEntryMapping.ToDomain)
            .OrderByDescending(x => x.PostingDate)
            .ThenByDescending(x => x.EntryId);

        return new(result.UserId, result.AccountId, result.Name, entries,
            (AccountLabel)result.AccountLabel, nextOlder, nextYounger);
    }

    private static BondAccountEntry ToBondAccountEntry(BondAccountEntryDto entry) => new(entry.AccountId, entry.EntryId, entry.PostingDate, entry.Value, entry.ValueChange, entry.BondDetailsId)
    {
        Description = entry.Description
    };
}