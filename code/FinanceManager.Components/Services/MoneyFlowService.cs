using FinanceManager.Components.Helpers;
using FinanceManager.Domain.Entities.MoneyFlowModels;
using FinanceManager.Domain.Enums;
using FinanceManager.Domain.Services;
using System.Net.Http.Json;

namespace FinanceManager.Components.Services;

public class MoneyFlowService(HttpClient httpClient) : IMoneyFlowService
{
    private readonly HttpClient _httpClient = httpClient;
    public async Task<List<TimeSeriesModel>> GetAssetsTimeSeries(int userId, DateTime start, DateTime end)
    {
        if (_httpClient is null) return [];

        var endpoint = $"{_httpClient.BaseAddress}api/MoneyFlow/GetEndAssetsPerAcount/{userId}/{start.ToRfc3339()}/{end.ToRfc3339()}";
        var result = await _httpClient.GetFromJsonAsync<List<TimeSeriesModel>>(endpoint);

        if (result is not null) return result;
        return [];
    }

    public async Task<List<TimeSeriesModel>> GetAssetsTimeSeries(int userId, DateTime start, DateTime end, InvestmentType investmentType)
    {
        if (_httpClient is null) return [];
        var result = await _httpClient.GetFromJsonAsync<List<TimeSeriesModel>>($"{_httpClient.BaseAddress}api/MoneyFlow/GetAssetsTimeSeries/{userId}/{start.ToRfc3339()}/{end.ToRfc3339()}/{investmentType}");

        if (result is not null) return result;
        return [];
    }

    public async Task<List<AssetEntry>> GetEndAssetsPerAcount(int userId, DateTime start, DateTime end)
    {
        if (_httpClient is null) return [];
        var result = await _httpClient.GetFromJsonAsync<List<AssetEntry>>($"{_httpClient.BaseAddress}api/MoneyFlow/GetEndAssetsPerAcount/{userId}/{start.ToRfc3339()}/{end.ToRfc3339()}");

        if (result is not null) return result;
        return [];
    }

    public async Task<List<AssetEntry>> GetEndAssetsPerType(int userId, DateTime start, DateTime end)
    {
        if (_httpClient is null) return [];
        var result = await _httpClient.GetFromJsonAsync<List<AssetEntry>>($"{_httpClient.BaseAddress}api/MoneyFlow/GetEndAssetsPerType/{userId}/{start.ToRfc3339()}/{end.ToRfc3339()}");

        if (result is not null) return result;
        return [];
    }

    public async Task<List<TimeSeriesModel>> GetIncome(int userId, DateTime start, DateTime end, TimeSpan? step = null)
    {
        if (_httpClient is null) return [];
        var result = await _httpClient.GetFromJsonAsync<List<TimeSeriesModel>>($"{_httpClient.BaseAddress}api/MoneyFlow/GetIncome/{userId}/{start.ToRfc3339()}/{end.ToRfc3339()}/{step}");

        if (result is not null) return result;
        return [];
    }

    public async Task<decimal?> GetNetWorth(int userId, DateTime date)
    {
        if (_httpClient is null) return default;
        var result = await _httpClient.GetFromJsonAsync<decimal?>($"{_httpClient.BaseAddress}api/MoneyFlow/GetNetWorth/{userId}/{date.ToRfc3339()}");

        if (result is not null) return result;
        return default;
    }

    public async Task<Dictionary<DateTime, decimal>> GetNetWorth(int userId, DateTime start, DateTime end)
    {
        if (_httpClient is null) return [];
        var result = await _httpClient.GetFromJsonAsync<Dictionary<DateTime, decimal>>($"{_httpClient.BaseAddress}api/MoneyFlow/GetNetWorth/{userId}/{start.ToRfc3339()}/{end.ToRfc3339()}");

        if (result is not null) return result;
        return [];
    }

    public async Task<List<TimeSeriesModel>> GetSpending(int userId, DateTime start, DateTime end, TimeSpan? step = null)
    {
        if (_httpClient is null) return [];
        var result = await _httpClient.GetFromJsonAsync<List<TimeSeriesModel>>($"{_httpClient.BaseAddress}api/MoneyFlow/GetSpending/{userId}/{start.ToRfc3339()}/{end.ToRfc3339()}/{step}");

        if (result is not null) return result;
        return [];
    }
}
