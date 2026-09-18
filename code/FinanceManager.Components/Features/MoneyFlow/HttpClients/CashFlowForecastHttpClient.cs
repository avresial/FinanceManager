using FinanceManager.Domain.MoneyFlow.Entities;
using System.Net.Http.Json;

namespace FinanceManager.Components.Features.MoneyFlow.HttpClients;

public class CashFlowForecastHttpClient(HttpClient httpClient)
{
    public Task<CashFlowForecast?> GetAsync(
        int userId,
        int currencyId,
        int horizonDays,
        CancellationToken cancellationToken = default) =>
        httpClient.GetFromJsonAsync<CashFlowForecast>(
            $"{httpClient.BaseAddress}api/CashFlowForecast?userId={userId}&currencyId={currencyId}&horizonDays={horizonDays}",
            cancellationToken);
}