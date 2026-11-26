// Test-only copy of the Components client that called the legacy account API. The Components client was removed with the
// account screens (#1142); the server routes stay until #1139 deletes them, so their tests keep this client.
using FinanceManager.Domain.FinancialAccounts.Currencies.Commands;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Shared.Commands;
using System.Net.Http.Json;

namespace FinanceManager.Tests.Integration.Support.LegacyAccountClients;

public class CurrencyEntryHttpClient(HttpClient httpClient)
{
    public async Task<CurrencyAccountEntry?> GetEntry(int accountId, long entryId)
    {
        try
        {
            var response = await httpClient.GetAsync($"{httpClient.BaseAddress}api/CurrencyEntry?accountId={accountId}&entryId={entryId}");
            if (response.StatusCode == System.Net.HttpStatusCode.NoContent) return null;
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
            return await response.Content.ReadFromJsonAsync<CurrencyAccountEntry?>();
        }
        catch (Exception)
        {
            return null;
        }
    }

    public async Task<DateTime?> GetOldestEntryDate(int accountId)
    {
        try
        {
            var response = await httpClient.GetAsync($"{httpClient.BaseAddress}api/CurrencyEntry/Oldest/{accountId}");
            if (response.StatusCode == System.Net.HttpStatusCode.NoContent) return null;
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
            return await response.Content.ReadFromJsonAsync<DateTime?>();
        }
        catch (Exception)
        {
            return null;
        }
    }

    public async Task<DateTime?> GetYoungestEntryDate(int accountId)
    {
        try
        {
            var response = await httpClient.GetAsync($"{httpClient.BaseAddress}api/CurrencyEntry/Youngest/{accountId}");
            if (response.StatusCode == System.Net.HttpStatusCode.NoContent) return null;
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
            return await response.Content.ReadFromJsonAsync<DateTime?>();
        }
        catch (Exception)
        {
            return null;
        }
    }

    public async Task<bool> AddEntryAsync(AddCurrencyAccountEntry addEntry)
    {
        var response = await httpClient.PostAsJsonAsync($"{httpClient.BaseAddress}api/CurrencyEntry", addEntry);
        if (response.IsSuccessStatusCode) return true;
        throw new Exception(await response.Content.ReadAsStringAsync());
    }

    public async Task<bool> DeleteEntryAsync(int accountId, long entryId)
    {
        var response = await httpClient.DeleteAsync($"{httpClient.BaseAddress}api/CurrencyEntry/{accountId}/{entryId}");
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> UpdateEntryAsync(CurrencyAccountEntry entry)
    {
        if (entry.AccountId is not int accountId) return false;
        var updateCommand = new UpdateCurrencyAccountEntry(
            accountId,
            entry.EntryId,
            entry.PostingDate,
            entry.Value,
            entry.ValueChange,
            entry.Description,
            entry.ContractorDetails,
            entry.Labels?.Select(l => new UpdateFinancialLabel(l.Id, l.Name)).ToList() ?? []
        );
        var response = await httpClient.PutAsJsonAsync($"{httpClient.BaseAddress}api/CurrencyEntry", updateCommand);
        return response.IsSuccessStatusCode;
    }
}