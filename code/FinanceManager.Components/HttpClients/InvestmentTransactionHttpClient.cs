using FinanceManager.Domain.FinancialAccounts.Investments.Dtos;
using System.Net;
using System.Net.Http.Json;

namespace FinanceManager.Components.HttpClients;

public class InvestmentTransactionHttpClient(HttpClient httpClient)
{
    public async Task<IReadOnlyList<InvestmentTransactionDto>> GetByAccountAsync(int accountId)
    {
        using var response = await httpClient.GetAsync($"{httpClient.BaseAddress}api/InvestmentTransaction/GetByAccount/{accountId}");
        if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.NoContent) return [];
        if (!response.IsSuccessStatusCode) return [];
        return await response.Content.ReadFromJsonAsync<List<InvestmentTransactionDto>>() ?? [];
    }

    public async Task<InvestmentTransactionDto?> GetAsync(long id)
    {
        using var response = await httpClient.GetAsync($"{httpClient.BaseAddress}api/InvestmentTransaction/Get/{id}");
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<InvestmentTransactionDto>();
    }

    public async Task<InvestmentTransactionDto?> AddAsync(AddInvestmentTransactionRequest request)
    {
        using var response = await httpClient.PostAsJsonAsync($"{httpClient.BaseAddress}api/InvestmentTransaction/Add", request);
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<InvestmentTransactionDto>();
    }

    public async Task<bool> UpdateAsync(UpdateInvestmentTransactionRequest request)
    {
        using var response = await httpClient.PutAsJsonAsync($"{httpClient.BaseAddress}api/InvestmentTransaction/Update", request);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> DeleteAsync(int accountId, long id)
    {
        using var response = await httpClient.DeleteAsync($"{httpClient.BaseAddress}api/InvestmentTransaction/Delete/{accountId}/{id}");
        return response.IsSuccessStatusCode;
    }
}