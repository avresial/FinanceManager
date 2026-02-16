using FinanceManager.Domain.Entities.Users;
using System.Net.Http.Json;

namespace FinanceManager.Components.HttpClients;

public class FinancialInsightsHttpClient(HttpClient httpClient)
{
    public async Task<List<FinancialInsight>> GetLatestAsync(int count = 3, int? accountId = null, CancellationToken cancellationToken = default)
    {
        var endpoint = $"{httpClient.BaseAddress}api/FinancialInsights/get-latest?count={count}";
        if (accountId.HasValue)
            endpoint += $"&accountId={accountId.Value}";
        try
        {
            var response = await httpClient.GetAsync(endpoint, cancellationToken);
            if (!response.IsSuccessStatusCode)
                return [];

            var result = await response.Content.ReadFromJsonAsync<List<FinancialInsight>>(cancellationToken: cancellationToken);
            return result ?? [];
        }
        catch (Exception)
        {
            return [];
        }
    }
}