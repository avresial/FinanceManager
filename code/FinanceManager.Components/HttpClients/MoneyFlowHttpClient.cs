using FinanceManager.Domain.Entities.Currencies;
using FinanceManager.Domain.Entities.MoneyFlowModels;
using System.Net.Http.Json;

namespace FinanceManager.Components.HttpClients;

public class MoneyFlowHttpClient(HttpClient httpClient)
{
    public Task<List<TimeSeriesModel>> GetClosingBalance(int userId, Currency currency, DateTime start, DateTime end) =>
        GetClosingBalance(userId, currency, start, end, []);

    public async Task<List<TimeSeriesModel>> GetClosingBalance(int userId, Currency currency, DateTime start, DateTime end, IReadOnlyCollection<int> accountIds)
    {
        string endpoint = AppendAccountIdsQuery($"{httpClient.BaseAddress}api/MoneyFlow/GetClosingBalance/{userId}/{currency.Id}/{start:O}/{end:O}", accountIds);
        var result = await httpClient.GetFromJsonAsync<List<TimeSeriesModel>>(endpoint);
        return result ?? [];
    }

    public Task<List<TimeSeriesModel>> GetInflow(int userId, Currency currency, DateTime start, DateTime end) =>
        GetInflow(userId, currency, start, end, []);

    public async Task<List<TimeSeriesModel>> GetInflow(int userId, Currency currency, DateTime start, DateTime end, IReadOnlyCollection<int> accountIds)
    {
        string endpoint = AppendAccountIdsQuery($"{httpClient.BaseAddress}api/MoneyFlow/GetInflow/{userId}/{currency.Id}/{start:O}/{end:O}", accountIds);
        var result = await httpClient.GetFromJsonAsync<List<TimeSeriesModel>>(endpoint);
        return result ?? [];
    }

    public Task<List<TimeSeriesModel>> GetNetCashFlow(int userId, Currency currency, DateTime start, DateTime end) =>
        GetNetCashFlow(userId, currency, start, end, []);

    public async Task<List<TimeSeriesModel>> GetNetCashFlow(int userId, Currency currency, DateTime start, DateTime end, IReadOnlyCollection<int> accountIds)
    {
        string endpoint = AppendAccountIdsQuery($"{httpClient.BaseAddress}api/MoneyFlow/GetNetCashFlow/{userId}/{currency.Id}/{start:O}/{end:O}", accountIds);
        var result = await httpClient.GetFromJsonAsync<List<TimeSeriesModel>>(endpoint);
        return result ?? [];
    }

    public async Task<decimal?> GetNetWorth(int userId, Currency currency, DateTime date)
    {
        var result = await httpClient.GetFromJsonAsync<decimal?>($"{httpClient.BaseAddress}api/MoneyFlow/GetNetWorth/{userId}/{currency.Id}/{date:O}");
        return result;
    }

    public async Task<Dictionary<DateTime, decimal>> GetNetWorth(int userId, Currency currency, DateTime start, DateTime end)
    {
        var result = await httpClient.GetFromJsonAsync<Dictionary<DateTime, decimal>>($"{httpClient.BaseAddress}api/MoneyFlow/GetNetWorth/{userId}/{currency.Id}/{start:O}/{end:O}");
        return result ?? [];
    }

    public Task<List<TimeSeriesModel>> GetOutflow(int userId, Currency currency, DateTime start, DateTime end) =>
        GetOutflow(userId, currency, start, end, []);

    public async Task<List<TimeSeriesModel>> GetOutflow(int userId, Currency currency, DateTime start, DateTime end, IReadOnlyCollection<int> accountIds)
    {
        string endpoint = AppendAccountIdsQuery($"{httpClient.BaseAddress}api/MoneyFlow/GetOutflow/{userId}/{currency.Id}/{start:O}/{end:O}", accountIds);
        var result = await httpClient.GetFromJsonAsync<List<TimeSeriesModel>>(endpoint);
        return result ?? [];
    }

    public async Task<List<NameValueResult>> GetLabelsValue(int userId, DateTime start, DateTime end)
    {
        string endpoint = $"{httpClient.BaseAddress}api/MoneyFlow/GetLabelsValue?userId={userId}&start={start:O}&end={end:O}";
        var result = await httpClient.GetFromJsonAsync<List<NameValueResult>>(endpoint);
        return result ?? [];
    }

    public async IAsyncEnumerable<InvestmentRate> GetInvestmentRate(int userId, DateTime start, DateTime end)
    {
        var results = await httpClient.GetFromJsonAsync<List<InvestmentRate>>($"{httpClient.BaseAddress}api/MoneyFlow/GetInvestmentRate?userId={userId}&start={start:O}&end={end:O}");
        if (results is null) yield break;
        foreach (var r in results) yield return r;
    }

    private static string AppendAccountIdsQuery(string endpoint, IReadOnlyCollection<int> accountIds)
    {
        if (accountIds.Count == 0) return endpoint;

        var query = string.Join("&", accountIds.Select(accountId => $"accountIds={accountId}"));
        return endpoint.Contains('?') ? $"{endpoint}&{query}" : $"{endpoint}?{query}";
    }
}