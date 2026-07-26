using FinanceManager.Domain.FinancialAccounts.Investments.Entities;
using FinanceManager.Domain.FinancialAccounts.Shared.Commands;
using FinanceManager.Domain.FinancialAccounts.Shared.ValueObjects;
using System.Net.Http.Json;

namespace FinanceManager.Components.Features.FinancialAccounts.HttpClients;

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

    public async Task<int?> AddAccountAsync(AddAccount addAccount)
    {
        var response = await httpClient.PostAsJsonAsync($"{httpClient.BaseAddress}api/InvestmentAccount/Add", addAccount);
        if (response.IsSuccessStatusCode) return await response.Content.ReadFromJsonAsync<int?>();
        throw new Exception(await response.Content.ReadAsStringAsync());
    }

    public async Task<bool> UpdateAccountAsync(UpdateAccount updateAccount)
    {
        var response = await httpClient.PutAsJsonAsync($"{httpClient.BaseAddress}api/InvestmentAccount/Update", updateAccount);
        return response.IsSuccessStatusCode;
    }

    public Task<bool> DeleteAccountAsync(int accountId) =>
        httpClient.DeleteFromJsonAsync<bool>($"{httpClient.BaseAddress}api/InvestmentAccount/Delete/{accountId}");
}