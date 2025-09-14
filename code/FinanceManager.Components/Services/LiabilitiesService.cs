using FinanceManager.Components.Helpers;
using FinanceManager.Domain.Entities.MoneyFlowModels;
using FinanceManager.Domain.Services;
using System.Net.Http.Json;

namespace FinanceManager.Components.Services;
public class LiabilitiesService(HttpClient httpClient) : ILiabilitiesService
{
    public async IAsyncEnumerable<NameValueResult> GetEndLiabilitiesPerAccount(int userId, DateTime start, DateTime end)
    {
        if (httpClient is null) yield break;
        var results = await httpClient.GetFromJsonAsync<List<NameValueResult>>($"{httpClient.BaseAddress}api/Liabilities/GetEndLiabilitiesPerAccount/{userId}/{start.ToRfc3339()}/{end.ToRfc3339()}");

        if (results is not null)
            foreach (var result in results)
                yield return result;

        yield break;
    }
    public async IAsyncEnumerable<NameValueResult> GetEndLiabilitiesPerType(int userId, DateTime start, DateTime end)
    {
        if (httpClient is null) yield break;
        var results = await httpClient.GetFromJsonAsync<List<NameValueResult>>($"{httpClient.BaseAddress}api/Liabilities/GetEndLiabilitiesPerType/{userId}/{start.ToRfc3339()}/{end.ToRfc3339()}");

        if (results is not null)
            foreach (var result in results)
                yield return result;

        yield break;
    }
    public async IAsyncEnumerable<TimeSeriesModel> GetLiabilitiesTimeSeries(int userId, DateTime start, DateTime end)
    {
        if (httpClient is null) yield break;
        var results = await httpClient.GetFromJsonAsync<List<TimeSeriesModel>>($"{httpClient.BaseAddress}api/Liabilities/GetLiabilitiesTimeSeries/{userId}/{start.ToRfc3339()}/{end.ToRfc3339()}");

        if (results is not null)
            foreach (var result in results)
                yield return result;

        yield break;
    }
    public async Task<bool> IsAnyAccountWithLiabilities(int userId)
    {
        if (httpClient is null) return default;

        return await httpClient.GetFromJsonAsync<bool>($"{httpClient.BaseAddress}api/Liabilities/IsAnyAccountWithLiabilities/{userId}");
    }
}
