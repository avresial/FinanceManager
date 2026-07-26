using Microsoft.Extensions.Logging;
using System.Net.Http.Json;

namespace FinanceManager.Components.Features.Administration.HttpClients;

public class NewVisitorsHttpClient(HttpClient httpClient, ILogger<NewVisitorsHttpClient> logger)
{
    public async Task AddVisit()
    {
        try
        {
            var response = await httpClient.PutAsync($"{httpClient.BaseAddress}api/NewVisitors", null);
            response.EnsureSuccessStatusCode();
        }
        catch (HttpRequestException ex)
        {
            // Visit tracking is non-essential telemetry; never let a failure surface to the UI.
            logger.LogWarning(ex, "Failed to record visit.");
        }
    }

    public async Task<int> GetVisit(DateTime dateTime)
    {
        try
        {
            var encodedDate = Uri.EscapeDataString(dateTime.Date.ToString("O"));
            return await httpClient.GetFromJsonAsync<int>($"{httpClient.BaseAddress}api/NewVisitors/GetNewVisitor/{encodedDate}");
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "Failed to retrieve visit count.");
            return 0;
        }
    }
}