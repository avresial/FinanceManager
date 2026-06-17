using FinanceManager.Domain.Dashboard.Dtos;
using System.Net.Http.Json;

namespace FinanceManager.Components.HttpClients;

/// <summary>
/// Reads the dashboard first-paint composite model from the single
/// <c>api/Dashboard/overview</c> endpoint so the dashboard can initialize its
/// state once instead of fanning out to the individual card-level endpoints.
/// </summary>
public class DashboardHttpClient(HttpClient httpClient)
{
    public async Task<DashboardOverviewDto?> GetOverview(int userId, int currencyId, DateTime start, DateTime end)
    {
        // Encode the round-trip dates: the "O" format contains '+' and ':', which a
        // query-string decoder would otherwise misread (e.g. '+' becomes a space).
        var encodedStart = Uri.EscapeDataString(start.ToString("O"));
        var encodedEnd = Uri.EscapeDataString(end.ToString("O"));

        string endpoint = $"{httpClient.BaseAddress}api/Dashboard/overview?userId={userId}&currencyId={currencyId}&start={encodedStart}&end={encodedEnd}";
        return await httpClient.GetFromJsonAsync<DashboardOverviewDto>(endpoint);
    }
}