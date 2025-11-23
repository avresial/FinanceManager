using FinanceManager.Domain.ValueObjects;
using System.Net.Http.Json;

namespace FinanceManager.Components.HttpClients;

public class InflationHttpClient(HttpClient httpClient)
{
    public async Task<InflationRate?> GetInflationRateAsync(int currencyId, DateOnly date, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.GetAsync($"{httpClient.BaseAddress}api/Inflation/{currencyId}/{date}", cancellationToken);
        
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            return null;
        
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<InflationRate>(cancellationToken: cancellationToken);
    }

    public async Task<IEnumerable<InflationRate>> GetInflationRatesAsync(int currencyId, DateOnly from, DateOnly to, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.GetAsync($"{httpClient.BaseAddress}api/Inflation/{currencyId}/range?from={from}&to={to}", cancellationToken);
        response.EnsureSuccessStatusCode();
        
        var result = await response.Content.ReadFromJsonAsync<IEnumerable<InflationRate>>(cancellationToken: cancellationToken);
        return result ?? [];
    }
}
