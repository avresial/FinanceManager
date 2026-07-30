using FinanceManager.Domain.FinancialAccounts.Investments.Dtos;
using System.Globalization;
using System.Net.Http.Json;

namespace FinanceManager.Components.Features.FinancialAccounts.HttpClients;

public class InvestmentValuationHttpClient(HttpClient httpClient)
{
    public async Task<IReadOnlyList<InvestmentTransactionValuationDto>> GetTransactionValuationsAsync(int accountId, int currencyId)
    {
        using var response = await httpClient.GetAsync(
            $"{httpClient.BaseAddress}api/InvestmentValuation/TransactionValuations/{accountId}/{currencyId}");
        if (!response.IsSuccessStatusCode) return [];
        return await response.Content.ReadFromJsonAsync<IReadOnlyList<InvestmentTransactionValuationDto>>() ?? [];
    }

    public async Task<IReadOnlyDictionary<long, decimal>> GetHoldingsAsync(int accountId, DateTime date)
    {
        using var response = await httpClient.GetAsync(
            $"{httpClient.BaseAddress}api/InvestmentValuation/Holdings/{accountId}/{Iso(date)}");
        if (!response.IsSuccessStatusCode) return new Dictionary<long, decimal>();
        return await response.Content.ReadFromJsonAsync<Dictionary<long, decimal>>() ?? [];
    }

    public async Task<decimal> GetValueAsync(int accountId, int currencyId, DateTime date)
    {
        using var response = await httpClient.GetAsync(
            $"{httpClient.BaseAddress}api/InvestmentValuation/Value/{accountId}/{currencyId}/{Iso(date)}");
        if (!response.IsSuccessStatusCode) return 0m;
        return await response.Content.ReadFromJsonAsync<decimal>();
    }

    public async Task<IReadOnlyDictionary<DateTime, decimal>> GetValueSeriesAsync(int accountId, int currencyId, DateTime start, DateTime end)
    {
        using var response = await httpClient.GetAsync(
            $"{httpClient.BaseAddress}api/InvestmentValuation/ValueSeries/{accountId}/{currencyId}/{Iso(start)}/{Iso(end)}");
        if (!response.IsSuccessStatusCode) return new Dictionary<DateTime, decimal>();
        return await response.Content.ReadFromJsonAsync<Dictionary<DateTime, decimal>>() ?? [];
    }

    public async Task<IReadOnlyDictionary<DateTime, decimal>> GetBenchmarkSeriesAsync(
        int accountId,
        int currencyId,
        DateTime start,
        DateTime end,
        long? listingId)
    {
        var url = $"{httpClient.BaseAddress}api/InvestmentValuation/BenchmarkSeries/"
            + $"{accountId}/{currencyId}/{Iso(start)}/{Iso(end)}";
        if (listingId is long id)
            url += $"?listingId={id}";

        using var response = await httpClient.GetAsync(url);
        if (!response.IsSuccessStatusCode) return new Dictionary<DateTime, decimal>();
        return await response.Content.ReadFromJsonAsync<Dictionary<DateTime, decimal>>() ?? [];
    }

    // Date-only ISO form so the value binds cleanly to the route's DateTime constraint.
    private static string Iso(DateTime date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}