using FinanceManager.Components.Helpers;
using FinanceManager.Domain.Entities.MoneyFlowModels;
using FinanceManager.Domain.Services;
using System.Net.Http.Json;

namespace FinanceManager.Components.Services;
public class LiabilitiesService(HttpClient httpClient) : ILiabilitiesService
{
    private readonly HttpClient _httpClient = httpClient;

    public async Task<List<PieChartModel>> GetEndLiabilitiesPerAccount(int userId, DateTime start, DateTime end)
    {
        if (_httpClient is null) return [];
        var result = await _httpClient.GetFromJsonAsync<List<PieChartModel>>($"{_httpClient.BaseAddress}api/Liabilities/GetEndLiabilitiesPerAccount/{userId}/{start.ToRfc3339()}/{end.ToRfc3339()}");

        if (result is not null) return result;
        return [];
    }
    public async Task<List<PieChartModel>> GetEndLiabilitiesPerType(int userId, DateTime start, DateTime end)
    {
        if (_httpClient is null) return [];
        var result = await _httpClient.GetFromJsonAsync<List<PieChartModel>>($"{_httpClient.BaseAddress}api/Liabilities/GetEndLiabilitiesPerType/{userId}/{start.ToRfc3339()}/{end.ToRfc3339()}");

        if (result is not null) return result;
        return [];
    }
    public async Task<List<TimeSeriesModel>> GetLiabilitiesTimeSeries(int userId, DateTime start, DateTime end)
    {
        if (_httpClient is null) return [];
        var result = await _httpClient.GetFromJsonAsync<List<TimeSeriesModel>>($"{_httpClient.BaseAddress}api/Liabilities/GetLiabilitiesTimeSeries/{userId}/{start.ToRfc3339()}/{end.ToRfc3339()}");
        if (result is not null) return result;
        return [];
    }
    public async Task<bool> IsAnyAccountWithLiabilities(int userId)
    {
        if (_httpClient is null) return default;

        return await _httpClient.GetFromJsonAsync<bool>($"{_httpClient.BaseAddress}api/Liabilities/IsAnyAccountWithLiabilities/{userId}");
    }
}
