using FinanceManager.Contracts.FinancialAccounts.Investments;
using System.Globalization;
using System.Net.Http.Json;

namespace FinanceManager.Components.Features.FinancialAccounts.HttpClients;

public class InvestmentValuationHttpClient(HttpClient httpClient)
{
    /// <summary>
    /// Per-transaction valuations of the caller's Buy trades among <paramref name="transactionIds"/> (linked to an account or not), in
    /// server order; the server accepts up to 200 ids per request. An empty list means nothing could
    /// be priced: a failed request throws instead, so the snapshot-backed details page can tell the
    /// two apart and keep the valuations it already painted. See docs/architecture/concepts/ui-snapshots.md.
    /// </summary>
    public async Task<IReadOnlyList<InvestmentTransactionValuationDto>> GetTransactionValuationsAsync(
        int currencyId,
        IReadOnlyCollection<long> transactionIds,
        CancellationToken cancellationToken = default)
    {
        if (transactionIds.Count == 0) return [];

        var query = "?" + string.Join("&", transactionIds.Select(id => $"transactionIds={id}"));
        using var response = await httpClient.GetAsync(
            $"{httpClient.BaseAddress}api/InvestmentValuation/TransactionValuations/{currencyId}{query}",
            cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<IReadOnlyList<InvestmentTransactionValuationDto>>(cancellationToken) ?? [];
    }

    public async Task<IReadOnlyDictionary<long, decimal>> GetHoldingsAsync(DateTime date)
    {
        using var response = await httpClient.GetAsync(
            $"{httpClient.BaseAddress}api/InvestmentValuation/Holdings/{Iso(date)}");
        if (!response.IsSuccessStatusCode) return new Dictionary<long, decimal>();
        return await response.Content.ReadFromJsonAsync<Dictionary<long, decimal>>() ?? [];
    }

    public async Task<IReadOnlyDictionary<DateTime, decimal>> GetValueSeriesAsync(int currencyId, DateTime start, DateTime end)
    {
        using var response = await httpClient.GetAsync(
            $"{httpClient.BaseAddress}api/InvestmentValuation/ValueSeries/{currencyId}/{Iso(start)}/{Iso(end)}");
        if (!response.IsSuccessStatusCode) return new Dictionary<DateTime, decimal>();
        return await response.Content.ReadFromJsonAsync<Dictionary<DateTime, decimal>>() ?? [];
    }

    public async Task<IReadOnlyDictionary<DateTime, decimal>> GetBenchmarkSeriesAsync(
        int currencyId,
        DateTime start,
        DateTime end,
        long? listingId)
    {
        var url = $"{httpClient.BaseAddress}api/InvestmentValuation/BenchmarkSeries/"
            + $"{currencyId}/{Iso(start)}/{Iso(end)}";
        if (listingId is long id)
            url += $"?listingId={id}";

        using var response = await httpClient.GetAsync(url);
        if (!response.IsSuccessStatusCode) return new Dictionary<DateTime, decimal>();
        return await response.Content.ReadFromJsonAsync<Dictionary<DateTime, decimal>>() ?? [];
    }

    // Date-only ISO form so the value binds cleanly to the route's DateTime constraint.
    private static string Iso(DateTime date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}