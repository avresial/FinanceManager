using System.Diagnostics;
using System.Net.Http.Json;

namespace FinanceManager.Components.HttpContexts;

public class NewVisitorsHttpContext(HttpClient httpClient)
{
    public async Task AddVisit()
    {
        var response = await httpClient.PutAsync($"{httpClient.BaseAddress}api/NewVisitors", null);
        response.EnsureSuccessStatusCode();
    }

    public Task<int> GetVisit(DateTime dateTime)
    {
        try
        {
            return httpClient.GetFromJsonAsync<int>($"{httpClient.BaseAddress}api/NewVisitors/GetNewVisitor/{dateTime.Date:O}");
        }
        catch (HttpRequestException ex)
        {
            Debug.WriteLine(ex.ToString());
            return Task.FromResult(0);
        }
    }
}
