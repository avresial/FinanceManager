using FinanceManager.Components.Helpers;
using FinanceManager.Domain.Entities.MoneyFlowModels;
using FinanceManager.Domain.Services;
using System.Net.Http.Json;

namespace FinanceManager.Components.Services;
public class MoneyFlowService(HttpClient httpClient) : IMoneyFlowService
{
    public async Task<List<TimeSeriesModel>> GetBalance(int userId, string currency, DateTime start, DateTime end)
    {
        if (httpClient is null) return [];

        string endpoint = $"{httpClient.BaseAddress}api/MoneyFlow/GetBalance/{userId}/{currency}/{start.ToRfc3339()}/{end.ToRfc3339()}";
        var result = await httpClient.GetFromJsonAsync<List<TimeSeriesModel>>(endpoint);

        if (result is not null) return result;
        return [];
    }
    public async Task<List<TimeSeriesModel>> GetIncome(int userId, string currency, DateTime start, DateTime end)
    {
        if (httpClient is null) return [];
        string endpoint = $"{httpClient.BaseAddress}api/MoneyFlow/GetIncome/{userId}/{currency}/{start.ToRfc3339()}/{end.ToRfc3339()}";

        var result = await httpClient.GetFromJsonAsync<List<TimeSeriesModel>>(endpoint);

        if (result is not null) return result;
        return [];
    }
    public async Task<decimal?> GetNetWorth(int userId, string currency, DateTime date)
    {
        if (httpClient is null) return default;

        var result = await httpClient.GetFromJsonAsync<decimal?>($"{httpClient.BaseAddress}api/MoneyFlow/GetNetWorth/{userId}/{currency}/{date.ToRfc3339()}");

        if (result is not null) return result;
        return default;
    }
    public async Task<Dictionary<DateTime, decimal>> GetNetWorth(int userId, string currency, DateTime start, DateTime end)
    {
        if (httpClient is null) return [];
        var result = await httpClient.GetFromJsonAsync<Dictionary<DateTime, decimal>>($"{httpClient.BaseAddress}api/MoneyFlow/GetNetWorth/{userId}/{currency}/{start.ToRfc3339()}/{end.ToRfc3339()}");

        if (result is not null) return result;
        return [];
    }
    public async Task<List<TimeSeriesModel>> GetSpending(int userId, string currency, DateTime start, DateTime end)
    {
        if (httpClient is null) return [];

        string endpoint = $"{httpClient.BaseAddress}api/MoneyFlow/GetSpending/{userId}/{currency}/{start.ToRfc3339()}/{end.ToRfc3339()}";

        var result = await httpClient.GetFromJsonAsync<List<TimeSeriesModel>>(endpoint);

        if (result is not null) return result;
        return [];
    }
    public async Task<List<NameValueResult>> GetLabelsValue(int userId, DateTime start, DateTime end)
    {
        if (httpClient is null) return [];

        string endpoint = $"{httpClient.BaseAddress}api/MoneyFlow/GetLabelsValue?userId={userId}&start={start.ToRfc3339()}&end={end.ToRfc3339()}";

        var result = await httpClient.GetFromJsonAsync<List<NameValueResult>>(endpoint);

        if (result is not null) return result;
        return [];
    }

    public async IAsyncEnumerable<InvestmentRate> GetInvestmentRate(int userId, DateTime start, DateTime end)
    {
        if (httpClient is null) yield break;
        var results = await httpClient.GetFromJsonAsync<List<InvestmentRate>>($"{httpClient.BaseAddress}api/MoneyFlow/GetInvestmentRate?userId={userId}&start={start.ToRfc3339()}&end={end.ToRfc3339()}");

        if (results is null) yield break;

        foreach (var result in results)
            yield return result;
    }
}