using FinanceManager.Domain.Identity.Entities;
using FinanceManager.Domain.Insights.Entities;
using System.Net.Http.Json;

namespace FinanceManager.Components.Features.Insights.HttpClients;

public class FinancialInsightsHttpClient(HttpClient httpClient)
{
    /// <summary>
    /// Reads the latest stored insights. Throws on a failed request — an empty list means the user
    /// genuinely has no insights, and callers that cache the result must be able to tell the two
    /// apart before overwriting what they already show.
    /// </summary>
    public async Task<List<FinancialInsight>> GetLatestAsync(int count = 3, int? accountId = null, CancellationToken cancellationToken = default)
    {
        var endpoint = $"{httpClient.BaseAddress}api/FinancialInsights/get-latest?count={count}";
        if (accountId.HasValue)
            endpoint += $"&accountId={accountId.Value}";

        var result = await httpClient.GetFromJsonAsync<List<FinancialInsight>>(endpoint, cancellationToken);
        return result ?? [];
    }

    public async Task<List<FinancialInsight>> GenerateAsync(int count = 3, int? accountId = null, CancellationToken cancellationToken = default)
    {
        var endpoint = $"{httpClient.BaseAddress}api/FinancialInsights/generate?count={count}";
        if (accountId.HasValue)
            endpoint += $"&accountId={accountId.Value}";

        try
        {
            var response = await httpClient.PostAsync(endpoint, content: null, cancellationToken);
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