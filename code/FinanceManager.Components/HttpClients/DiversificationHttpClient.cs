using FinanceManager.Domain.Insights.Entities;
using FinanceManager.Domain.MoneyFlow.Entities;
using System.Net.Http.Json;

namespace FinanceManager.Components.HttpClients;

public class DiversificationHttpClient(HttpClient httpClient)
{
    public async Task<DiversificationScore?> GetDiversificationScore(int userId, DateTime asOfDate)
    {
        var result = await httpClient.GetFromJsonAsync<DiversificationScore>(
            $"{httpClient.BaseAddress}api/Diversification/{userId}/{asOfDate:O}");
        return result;
    }

    public async Task<DiversificationBreakdown?> GetDiversificationBreakdown(int userId, DateTime asOfDate)
    {
        var result = await httpClient.GetFromJsonAsync<DiversificationBreakdown>(
            $"{httpClient.BaseAddress}api/Diversification/{userId}/{asOfDate:O}/breakdown");
        return result;
    }
}