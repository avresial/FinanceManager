using FinanceManager.Components.Helpers;
using System.Diagnostics;
using System.Net.Http.Json;

namespace FinanceManager.Components.Services;
public class NewVisitorsService(HttpClient httpClient)
{
    private readonly HttpClient _httpClient = httpClient;
    public async Task AddVisit()
    {
        var test = await _httpClient.PutAsync($"{_httpClient.BaseAddress}api/NewVisitors", null);

        Debug.WriteLine(test);
    }

    public async Task<int> GetVisit(DateTime dateTime)
    {
        return await _httpClient.GetFromJsonAsync<int>($"{_httpClient.BaseAddress}api/NewVisitors/GetNewVisitor/{dateTime.Date.ToRfc3339()}");
    }
}
