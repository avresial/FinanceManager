using FinanceManager.Domain.Entities.MoneyFlowModels;
using System.Net.Http.Json;

namespace FinanceManager.Components.HttpContexts;

public class LiabilitiesHttpContext(HttpClient httpClient)
{
    public async IAsyncEnumerable<NameValueResult> GetEndLiabilitiesPerAccount(int userId, DateTime start, DateTime end)
    {
        var results = await httpClient.GetFromJsonAsync<List<NameValueResult>>($"{httpClient.BaseAddress}api/Liabilities/GetEndLiabilitiesPerAccount/{userId}/{start:O}/{end:O}");
        if (results is null) yield break;
        foreach (var r in results) yield return r;
    }

    public async IAsyncEnumerable<NameValueResult> GetEndLiabilitiesPerType(int userId, DateTime start, DateTime end)
    {
        var results = await httpClient.GetFromJsonAsync<List<NameValueResult>>($"{httpClient.BaseAddress}api/Liabilities/GetEndLiabilitiesPerType/{userId}/{start:O}/{end:O}");
        if (results is null) yield break;
        foreach (var r in results) yield return r;
    }

    public async IAsyncEnumerable<TimeSeriesModel> GetLiabilitiesTimeSeries(int userId, DateTime start, DateTime end)
    {
        var results = await httpClient.GetFromJsonAsync<List<TimeSeriesModel>>($"{httpClient.BaseAddress}api/Liabilities/GetLiabilitiesTimeSeries/{userId}/{start:O}/{end:O}");
        if (results is null) yield break;
        foreach (var r in results) yield return r;
    }

    public async Task<bool> IsAnyAccountWithLiabilities(int userId)
    {
        return await httpClient.GetFromJsonAsync<bool>($"{httpClient.BaseAddress}api/Liabilities/IsAnyAccountWithLiabilities/{userId}");
    }
}
