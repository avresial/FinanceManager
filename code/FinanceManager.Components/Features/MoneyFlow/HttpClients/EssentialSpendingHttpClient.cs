using FinanceManager.Domain.Currencies.Entities;
using FinanceManager.Domain.MoneyFlow.Entities;
using System.Net.Http.Json;

namespace FinanceManager.Components.Features.MoneyFlow.HttpClients;

public class EssentialSpendingHttpClient(HttpClient httpClient)
{
    public async Task<List<TimeSeriesModel>> GetEssentialSpending(int userId, Currency currency, DateTime start, DateTime end)
    {
        var result = await httpClient.GetFromJsonAsync<List<TimeSeriesModel>>($"{httpClient.BaseAddress}api/EssentialSpending/GetEssentialSpending/{userId}/{currency.Id}/{start:O}/{end:O}");
        return result ?? [];
    }
}