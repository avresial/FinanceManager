using FinanceManager.Domain.Entities.Currencies;
using FinanceManager.Domain.Entities.MoneyFlowModels;
using System.Net.Http.Json;

namespace FinanceManager.Components.HttpClients;

public class MoneyFlowHttpClient(HttpClient httpClient)
{
    public async Task<List<TimeSeriesModel>> GetBalance(int userId, Currency currency, DateTime start, DateTime end)
    {
        string endpoint = $"{httpClient.BaseAddress}api/MoneyFlow/GetBalance/{userId}/{currency.Id}/{start:O}/{end:O}";
        var result = await httpClient.GetFromJsonAsync<List<TimeSeriesModel>>(endpoint);
        return result ?? new List<TimeSeriesModel>();
    }

    public async Task<List<TimeSeriesModel>> GetIncome(int userId, Currency currency, DateTime start, DateTime end)
    {
        string endpoint = $"{httpClient.BaseAddress}api/MoneyFlow/GetIncome/{userId}/{currency.Id}/{start:O}/{end:O}";
        var result = await httpClient.GetFromJsonAsync<List<TimeSeriesModel>>(endpoint);
        return result ?? new List<TimeSeriesModel>();
    }

    public async Task<decimal?> GetNetWorth(int userId, Currency currency, DateTime date)
    {
        var result = await httpClient.GetFromJsonAsync<decimal?>($"{httpClient.BaseAddress}api/MoneyFlow/GetNetWorth/{userId}/{currency.Id}/{date:O}");
        return result;
    }

    public async Task<Dictionary<DateTime, decimal>> GetNetWorth(int userId, Currency currency, DateTime start, DateTime end)
    {
        var result = await httpClient.GetFromJsonAsync<Dictionary<DateTime, decimal>>($"{httpClient.BaseAddress}api/MoneyFlow/GetNetWorth/{userId}/{currency.Id}/{start:O}/{end:O}");
        return result ?? new Dictionary<DateTime, decimal>();
    }

    public async Task<List<TimeSeriesModel>> GetSpending(int userId, Currency currency, DateTime start, DateTime end)
    {
        string endpoint = $"{httpClient.BaseAddress}api/MoneyFlow/GetSpending/{userId}/{currency.Id}/{start:O}/{end:O}";
        var result = await httpClient.GetFromJsonAsync<List<TimeSeriesModel>>(endpoint);
        return result ?? new List<TimeSeriesModel>();
    }

    public async Task<List<NameValueResult>> GetLabelsValue(int userId, DateTime start, DateTime end)
    {
        string endpoint = $"{httpClient.BaseAddress}api/MoneyFlow/GetLabelsValue?userId={userId}&start={start:O}&end={end:O}";
        var result = await httpClient.GetFromJsonAsync<List<NameValueResult>>(endpoint);
        return result ?? new List<NameValueResult>();
    }

    public async IAsyncEnumerable<InvestmentRate> GetInvestmentRate(int userId, DateTime start, DateTime end)
    {
        var results = await httpClient.GetFromJsonAsync<List<InvestmentRate>>($"{httpClient.BaseAddress}api/MoneyFlow/GetInvestmentRate?userId={userId}&start={start:O}&end={end:O}");
        if (results is null) yield break;
        foreach (var r in results) yield return r;
    }
}
