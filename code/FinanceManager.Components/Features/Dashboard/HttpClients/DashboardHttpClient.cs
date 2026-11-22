using FinanceManager.Domain.Dashboard.Dtos;
using System.Globalization;
using System.Net.Http.Json;

namespace FinanceManager.Components.Features.Dashboard.HttpClients;

/// <summary>
/// Reads the dashboard first-paint composite model from the single
/// <c>api/Dashboard/overview</c> endpoint so the dashboard can initialize its
/// state once instead of fanning out to the individual card-level endpoints.
/// </summary>
public class DashboardHttpClient(HttpClient httpClient)
{
    public async Task<DashboardOverviewDto?> GetOverview(int userId, int currencyId, DateTime start, DateTime end)
    {
        var wholeUtcDays = start.Kind == DateTimeKind.Utc && end.Kind == DateTimeKind.Utc
            && start.TimeOfDay == TimeSpan.Zero && TimeOnly.FromDateTime(end) == TimeOnly.MaxValue;
        var format = wholeUtcDays ? "yyyy-MM-dd" : "O";
        var encodedStart = Uri.EscapeDataString(start.ToString(format, CultureInfo.InvariantCulture));
        var encodedEnd = Uri.EscapeDataString(end.ToString(format, CultureInfo.InvariantCulture));

        string endpoint = $"{httpClient.BaseAddress}api/Dashboard/overview?userId={userId}&currencyId={currencyId}&start={encodedStart}&end={encodedEnd}";
        return await httpClient.GetFromJsonAsync<DashboardOverviewDto>(endpoint);
    }
}