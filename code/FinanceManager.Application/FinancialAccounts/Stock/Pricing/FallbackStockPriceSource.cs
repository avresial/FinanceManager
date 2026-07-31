using FinanceManager.Domain.Assets.Entities;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Investments.Entities;
using Microsoft.Extensions.Logging;

namespace FinanceManager.Application.FinancialAccounts.Stock.Pricing;

/// <summary>
/// Composes an ordered list of <see cref="IStockPriceSource"/> providers into a single source.
/// Tries each in order and returns the first non-empty result, so a rate-limited or unentitled
/// primary (Alpha Vantage) transparently falls through to a secondary (EODHD).
/// </summary>
public sealed class FallbackStockPriceSource(
    IReadOnlyList<IStockPriceSource> sources,
    ILogger<FallbackStockPriceSource> logger) : IStockPriceSource
{
    public string Name => "Fallback";
    public int Priority => 0;
    public bool SupportsProviderSelection => true;
    public IReadOnlySet<AssetType> SupportedAssetTypes => sources.SelectMany(x => x.SupportedAssetTypes).ToHashSet();
    public IReadOnlySet<string> SupportedIntervals => sources.SelectMany(x => x.SupportedIntervals).ToHashSet(StringComparer.OrdinalIgnoreCase);

    public async Task<IReadOnlyList<StockPrice>> GetDailySeries(string symbol, string isin, DateTime start, DateTime end, Currency currency, CancellationToken ct = default)
    {
        var ordered = sources.OrderBy(x => x.Priority).ToList();
        for (var i = 0; i < ordered.Count; i++)
        {
            var source = ordered[i];
            ct.ThrowIfCancellationRequested();

            var prices = await source.GetDailySeries(symbol, isin, start, end, currency, ct);
            if (prices.Count > 0)
            {
                if (i > 0)
                    logger.LogInformation("Price source {Source} served {Symbol} after {Tried} earlier source(s) returned no data.", source.Name, Sanitize(symbol), i);
                return prices;
            }

            logger.LogDebug("Price source {Source} returned no data for {Symbol}; trying next.", source.Name, Sanitize(symbol));
        }

        return [];
    }

    public async Task<StockPrice?> GetLatestQuote(
        string symbol,
        string isin,
        Currency currency,
        CancellationToken ct = default)
    {
        foreach (var source in sources.OrderBy(x => x.Priority))
        {
            var quote = await source.GetLatestQuote(symbol, isin, currency, ct);
            if (quote is not null)
                return quote;
        }

        return null;
    }

    public Task<IReadOnlyList<StockPrice>> GetDailySeries(
        MarketDataProvider provider,
        string symbol,
        string isin,
        DateTime start,
        DateTime end,
        Currency currency,
        CancellationToken ct = default)
    {
        var source = sources
            .Where(x => x.Provider == provider)
            .OrderBy(x => x.Priority)
            .FirstOrDefault();

        return source is null
            ? Task.FromResult<IReadOnlyList<StockPrice>>([])
            : source.GetDailySeries(symbol, isin, start, end, currency, ct);
    }

    public int GetPriority(MarketDataProvider provider) =>
        sources.Where(x => x.Provider == provider).Select(x => x.Priority).DefaultIfEmpty(int.MaxValue).Min();

    // Strip CR/LF so an attacker-influenced symbol cannot forge log entries (log injection).
    private static string Sanitize(string value)
        => value.Replace("\r", string.Empty).Replace("\n", string.Empty);
}