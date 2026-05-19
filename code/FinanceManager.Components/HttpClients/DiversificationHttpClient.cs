using FinanceManager.Domain.Entities.MoneyFlowModels;
using System.Net.Http.Json;

namespace FinanceManager.Components.HttpClients;

public class DiversificationHttpClient(HttpClient httpClient)
{
    public async Task<DiversificationScore?> GetDiversificationScore(int userId, DateTime asOfDate)
    {
        try
        {
            return await httpClient.GetFromJsonAsync<DiversificationScore>(
                $"{httpClient.BaseAddress}api/Diversification/{userId}/{asOfDate:O}");
        }
        catch
        {
            return null;
        }
    }
}
