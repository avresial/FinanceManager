using FinanceManager.Domain.Entities.Currencies;
using FinanceManager.Domain.Entities.MoneyFlowModels;
using System.Net.Http.Json;

namespace FinanceManager.Components.HttpClients;

public class EssentialSpendingHttpClient(HttpClient httpClient)
{
    public Task<List<TimeSeriesModel>> GetEssentialSpending(int userId, Currency currency, DateTime start, DateTime end) =>
        GetEssentialSpending(userId, currency, start, end, []);

    public async Task<List<TimeSeriesModel>> GetEssentialSpending(int userId, Currency currency, DateTime start, DateTime end, IReadOnlyCollection<int> accountIds)
    {
        string endpoint = AppendAccountIdsQuery($"{httpClient.BaseAddress}api/EssentialSpending/GetEssentialSpending/{userId}/{currency.Id}/{start:O}/{end:O}", accountIds);
        var result = await httpClient.GetFromJsonAsync<List<TimeSeriesModel>>(endpoint);
        return result ?? [];
    }

    private static string AppendAccountIdsQuery(string endpoint, IReadOnlyCollection<int> accountIds)
    {
        if (accountIds.Count == 0) return endpoint;

        var query = string.Join("&", accountIds.Select(accountId => $"accountIds={accountId}"));
        return endpoint.Contains('?') ? $"{endpoint}&{query}" : $"{endpoint}?{query}";
    }
}