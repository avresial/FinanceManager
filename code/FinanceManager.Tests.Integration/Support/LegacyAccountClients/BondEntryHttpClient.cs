// Test-only copy of the Components client that called the legacy account API. The Components client was removed with the
// account screens (#1142); the server routes stay until #1139 deletes them, so their tests keep this client.
using FinanceManager.Domain.FinancialAccounts.Bond.Commands;
using FinanceManager.Domain.FinancialAccounts.Bond.Entities;
using FinanceManager.Domain.FinancialAccounts.Shared.Commands;
using System.Net.Http.Json;

namespace FinanceManager.Tests.Integration.Support.LegacyAccountClients;

public class BondEntryHttpClient(HttpClient httpClient)
{
    public async Task<DateTime?> GetOldestEntryDate(int accountId)
    {
        var response = await httpClient.GetAsync($"{httpClient.BaseAddress}api/BondEntry/Oldest/{accountId}");
        if (response.StatusCode == System.Net.HttpStatusCode.NoContent) return null;
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
        return await response.Content.ReadFromJsonAsync<DateTime?>();
    }

    public async Task<DateTime?> GetYoungestEntryDate(int accountId)
    {
        var response = await httpClient.GetAsync($"{httpClient.BaseAddress}api/BondEntry/Youngest/{accountId}");
        if (response.StatusCode == System.Net.HttpStatusCode.NoContent) return null;
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
        return await response.Content.ReadFromJsonAsync<DateTime?>();
    }
}