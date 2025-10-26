using FinanceManager.Domain.Entities;
using FinanceManager.Domain.Entities.MoneyFlowModels;
using FinanceManager.Domain.Enums;
using System.Net.Http.Json;

namespace FinanceManager.Components.HttpContexts;

public class AssetsHttpContext(HttpClient httpClient)
{
    public async Task<bool> IsAnyAccountWithAssets(int userId)
    {
        try
        {
            return await httpClient.GetFromJsonAsync<bool>($"{httpClient.BaseAddress}api/Assets/IsAnyAccountWithAssets/{userId}");
        }
        catch
        {
            return false;
        }
    }

    public async Task<List<TimeSeriesModel>> GetAssetsTimeSeries(int userId, Currency currency, DateTime start, DateTime end)
    {
        var result = await httpClient.GetFromJsonAsync<List<TimeSeriesModel>>($"{httpClient.BaseAddress}api/Assets/GetAssetsTimeSeries/{userId}/{currency}/{start:O}/{end:O}");
        return result ?? new List<TimeSeriesModel>();
    }

    public async Task<List<TimeSeriesModel>> GetAssetsTimeSeries(int userId, Currency currency, DateTime start, DateTime end, InvestmentType investmentType)
    {
        var result = await httpClient.GetFromJsonAsync<List<TimeSeriesModel>>($"{httpClient.BaseAddress}api/Assets/GetAssetsTimeSeries/{userId}/{currency}/{start:O}/{end:O}/{investmentType}");
        return result ?? new List<TimeSeriesModel>();
    }

    public async Task<List<NameValueResult>> GetEndAssetsPerAccount(int userId, Currency currency, DateTime start, DateTime end)
    {
        var result = await httpClient.GetFromJsonAsync<List<NameValueResult>>($"{httpClient.BaseAddress}api/Assets/GetEndAssetsPerAccount/{userId}/{currency}/{start:O}/{end:O}");
        return result ?? new List<NameValueResult>();
    }

    public async Task<List<NameValueResult>> GetEndAssetsPerType(int userId, Currency currency, DateTime start, DateTime end)
    {
        var result = await httpClient.GetFromJsonAsync<List<NameValueResult>>($"{httpClient.BaseAddress}api/Assets/GetEndAssetsPerType/{userId}/{currency}/{start:O}/{end:O}");
        return result ?? new List<NameValueResult>();
    }
}
