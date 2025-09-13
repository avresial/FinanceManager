using FinanceManager.Components.Helpers;
using System.Diagnostics;
using System.Net.Http.Json;

namespace FinanceManager.Components.Services;
public class NewVisitorsService(HttpClient httpClient)
{
    private readonly HttpClient _httpClient = httpClient;
    public async Task AddVisit()
    {
        var response = await _httpClient.PutAsync($"{_httpClient.BaseAddress}api/NewVisitors", null);
        response.EnsureSuccessStatusCode();
    }

    public Task<int> GetVisit(DateTime dateTime)
    {
        try
        {
            return _httpClient.GetFromJsonAsync<int>($"{_httpClient.BaseAddress}api/NewVisitors/GetNewVisitor/{dateTime.Date.ToRfc3339()}");
        }
        catch (HttpRequestException ex)
        {
            Debug.WriteLine(ex.ToString());
            return Task.FromResult(0);
        }
    }
}
