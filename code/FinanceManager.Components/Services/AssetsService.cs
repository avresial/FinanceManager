using FinanceManager.Components.Helpers;
using FinanceManager.Domain.Entities.MoneyFlowModels;
using FinanceManager.Domain.Enums;
using FinanceManager.Domain.Services;
using Microsoft.Extensions.Logging;
using System.Net.Http.Json;

namespace FinanceManager.Components.Services;

public class AssetsService(HttpClient httpClient, ILogger<AssetsService> logger) : IAssetsService
{
    public async Task<bool> IsAnyAccountWithAssets(int userId)
    {
        if (httpClient is null) return default;
        try
        {
            return await httpClient.GetFromJsonAsync<bool>($"{httpClient.BaseAddress}api/Assets/IsAnyAccountWithAssets/{userId}");
        }
        catch (Exception ex)
        {
            logger.LogError(ex.Message, ex);
            return false;
        }
    }
    public async Task<List<TimeSeriesModel>> GetAssetsTimeSeries(int userId, string currency, DateTime start, DateTime end)
    {
        if (httpClient is null) return [];

        var result = await httpClient.GetFromJsonAsync<List<TimeSeriesModel>>($"{httpClient.BaseAddress}api/Assets/GetAssetsTimeSeries/{userId}/{currency}/{start.ToRfc3339()}/{end.ToRfc3339()}");

        if (result is not null) return result;
        return [];
    }
    public async Task<List<TimeSeriesModel>> GetAssetsTimeSeries(int userId, string currency, DateTime start, DateTime end, InvestmentType investmentType)
    {
        if (httpClient is null) return [];
        var result = await httpClient.GetFromJsonAsync<List<TimeSeriesModel>>($"{httpClient.BaseAddress}api/Assets/GetAssetsTimeSeries/{userId}/{currency}/{start.ToRfc3339()}/{end.ToRfc3339()}/{investmentType}");

        if (result is not null) return result;
        return [];
    }
    public async Task<List<NameValueResult>> GetEndAssetsPerAccount(int userId, string currency, DateTime start, DateTime end)
    {
        if (httpClient is null) return [];
        var result = await httpClient.GetFromJsonAsync<List<NameValueResult>>($"{httpClient.BaseAddress}api/Assets/GetEndAssetsPerAccount/{userId}/{currency}/{start.ToRfc3339()}/{end.ToRfc3339()}");

        if (result is not null) return result;
        return [];
    }
    public async Task<List<NameValueResult>> GetEndAssetsPerType(int userId, string currency, DateTime start, DateTime end)
    {
        if (httpClient is null) return [];
        var result = await httpClient.GetFromJsonAsync<List<NameValueResult>>($"{httpClient.BaseAddress}api/Assets/GetEndAssetsPerType/{userId}/{currency}/{start.ToRfc3339()}/{end.ToRfc3339()}");

        if (result is not null) return result;
        return [];
    }
}
