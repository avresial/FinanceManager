using System.Diagnostics;
using System.Net.Http.Json;

namespace FinanceManager.Components.HttpClients;

public class NewVisitorsHttpClient(HttpClient httpClient)
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
            var encodedDate = Uri.EscapeDataString(dateTime.Date.ToString("O"));
            return httpClient.GetFromJsonAsync<int>($"{httpClient.BaseAddress}api/NewVisitors/GetNewVisitor/{encodedDate}");
        }
        catch (HttpRequestException ex)
        {
            Debug.WriteLine(ex.ToString());
            return Task.FromResult(0);
        }
    }
}