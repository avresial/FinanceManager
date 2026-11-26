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
}