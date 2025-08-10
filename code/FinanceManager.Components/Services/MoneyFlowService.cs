using FinanceManager.Components.Helpers;
using FinanceManager.Domain.Entities.MoneyFlowModels;
using FinanceManager.Domain.Enums;
using FinanceManager.Domain.Services;
using System.Net.Http.Json;

namespace FinanceManager.Components.Services;

public class MoneyFlowService(HttpClient httpClient) : IMoneyFlowService
{
    private readonly HttpClient _httpClient = httpClient;
    public async Task<bool> IsAnyAccountWithAssets(int userId)
    {
        if (_httpClient is null) return default;
        try
        {
            return await _httpClient.GetFromJsonAsync<bool>($"{_httpClient.BaseAddress}api/MoneyFlow/IsAnyAccountWithAssets/{userId}");
        }
        catch (Exception ex)
        {
            return false;
        }
    }
    public async Task<List<TimeSeriesModel>> GetAssetsTimeSeries(int userId, string currency, DateTime start, DateTime end)
    {
        if (_httpClient is null) return [];

        var result = await _httpClient.GetFromJsonAsync<List<TimeSeriesModel>>($"{_httpClient.BaseAddress}api/MoneyFlow/GetAssetsTimeSeries/{userId}/{currency}/{start.ToRfc3339()}/{end.ToRfc3339()}");

        if (result is not null) return result;
        return [];
    }
    public async Task<List<TimeSeriesModel>> GetAssetsTimeSeries(int userId, string currency, DateTime start, DateTime end, InvestmentType investmentType)
    {
        if (_httpClient is null) return [];
        var result = await _httpClient.GetFromJsonAsync<List<TimeSeriesModel>>($"{_httpClient.BaseAddress}api/MoneyFlow/GetAssetsTimeSeries/{userId}/{currency}/{start.ToRfc3339()}/{end.ToRfc3339()}/{investmentType}");

        if (result is not null) return result;
        return [];
    }
    public async Task<List<NameValueResult>> GetEndAssetsPerAccount(int userId, string currency, DateTime start, DateTime end)
    {
        if (_httpClient is null) return [];
        var result = await _httpClient.GetFromJsonAsync<List<NameValueResult>>($"{_httpClient.BaseAddress}api/MoneyFlow/GetEndAssetsPerAccount/{userId}/{currency}/{start.ToRfc3339()}/{end.ToRfc3339()}");

        if (result is not null) return result;
        return [];
    }
    public async Task<List<NameValueResult>> GetEndAssetsPerType(int userId, string currency, DateTime start, DateTime end)
    {
        if (_httpClient is null) return [];
        var result = await _httpClient.GetFromJsonAsync<List<NameValueResult>>($"{_httpClient.BaseAddress}api/MoneyFlow/GetEndAssetsPerType/{userId}/{currency}/{start.ToRfc3339()}/{end.ToRfc3339()}");

        if (result is not null) return result;
        return [];
    }
    public async Task<List<TimeSeriesModel>> GetBalance(int userId, string currency, DateTime start, DateTime end, TimeSpan? step = null)
    {
        if (_httpClient is null) return [];

        string endpoint = $"{_httpClient.BaseAddress}api/MoneyFlow/GetBalance/{userId}/{currency}/{start.ToRfc3339()}/{end.ToRfc3339()}";
        if (step is not null) endpoint += $"/{step.Value.Ticks}";
        var result = await _httpClient.GetFromJsonAsync<List<TimeSeriesModel>>(endpoint);

        if (result is not null) return result;
        return [];
    }
    public async Task<List<TimeSeriesModel>> GetIncome(int userId, string currency, DateTime start, DateTime end, TimeSpan? step = null)
    {
        if (_httpClient is null) return [];
        var test = end.ToRfc3339();
        string endpoint = $"{_httpClient.BaseAddress}api/MoneyFlow/GetIncome/{userId}/{currency}/{start.ToRfc3339()}/{end.ToRfc3339()}";
        if (step is not null) endpoint += $"/{step.Value.Ticks}";

        var result = await _httpClient.GetFromJsonAsync<List<TimeSeriesModel>>(endpoint);

        if (result is not null) return result;
        return [];
    }
    public async Task<decimal?> GetNetWorth(int userId, string currency, DateTime date)
    {
        if (_httpClient is null) return default;

        var result = await _httpClient.GetFromJsonAsync<decimal?>($"{_httpClient.BaseAddress}api/MoneyFlow/GetNetWorth/{userId}/{currency}/{date.ToRfc3339()}");

        if (result is not null) return result;
        return default;
    }
    public async Task<Dictionary<DateTime, decimal>> GetNetWorth(int userId, string currency, DateTime start, DateTime end)
    {
        if (_httpClient is null) return [];
        var result = await _httpClient.GetFromJsonAsync<Dictionary<DateTime, decimal>>($"{_httpClient.BaseAddress}api/MoneyFlow/GetNetWorth/{userId}/{currency}/{start.ToRfc3339()}/{end.ToRfc3339()}");

        if (result is not null) return result;
        return [];
    }
    public async Task<List<TimeSeriesModel>> GetSpending(int userId, string currency, DateTime start, DateTime end, TimeSpan? step = null)
    {
        if (_httpClient is null) return [];

        string endpoint = $"{_httpClient.BaseAddress}api/MoneyFlow/GetSpending/{userId}/{currency}/{start.ToRfc3339()}/{end.ToRfc3339()}";
        if (step is not null) endpoint += $"/{step.Value.Ticks}";

        var result = await _httpClient.GetFromJsonAsync<List<TimeSeriesModel>>(endpoint);

        if (result is not null) return result;
        return [];
    }
    public async Task<List<NameValueResult>> GetLabelsValue(int userId, DateTime start, DateTime end, TimeSpan? step = null)
    {
        if (_httpClient is null) return [];

        string endpoint = $"{_httpClient.BaseAddress}api/MoneyFlow/GetLabelsValue?userId={userId}&start={start.ToRfc3339()}&end={end.ToRfc3339()}";
        if (step is not null) endpoint += $"/{step.Value.Ticks}";

        var result = await _httpClient.GetFromJsonAsync<List<NameValueResult>>(endpoint);

        if (result is not null) return result;
        return [];
    }


}