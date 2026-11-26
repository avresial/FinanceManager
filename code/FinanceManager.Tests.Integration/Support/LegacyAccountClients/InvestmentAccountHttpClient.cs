// Test-only copy of the Components client that called the legacy account API. The Components client was removed with the
// account screens (#1142); the server routes stay until #1139 deletes them, so their tests keep this client.
using FinanceManager.Domain.FinancialAccounts.Investments.Entities;
using FinanceManager.Domain.FinancialAccounts.Shared.Commands;
using FinanceManager.Domain.FinancialAccounts.Shared.ValueObjects;
using System.Net.Http.Json;

namespace FinanceManager.Tests.Integration.Support.LegacyAccountClients;

public class InvestmentAccountHttpClient(HttpClient httpClient)
{
    public async Task<List<AvailableAccount>> GetAvailableAccountsAsync()
    {
        try
        {
            var response = await httpClient.GetAsync($"{httpClient.BaseAddress}api/InvestmentAccount");
            var result = await response.Content.ReadFromJsonAsync<List<AvailableAccount>>();
            return result ?? [];
        }
        catch (Exception)
        {
        }

        return [];
    }

    public async Task<InvestmentAccount?> GetAccountAsync(int accountId)
    {
        var response = await httpClient.GetAsync($"{httpClient.BaseAddress}api/InvestmentAccount/{accountId}");
        if (!response.IsSuccessStatusCode || response.Content.Headers.ContentLength is null or 0)
            return null;

        return await response.Content.ReadFromJsonAsync<InvestmentAccount>();
    }
}