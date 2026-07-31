using FinanceManager.Domain.Assets.Entities;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Investments.Entities;

namespace FinanceManager.Application.FinancialAccounts.Stock.Pricing;

/// <summary>
/// Provider-agnostic source of daily stock prices. Implemented by each external price
/// provider (Alpha Vantage, EODHD, …) and by the fallback chain that composes them, so
/// <see cref="StockPriceProvider"/> can fetch prices without binding to a single vendor.
/// </summary>
public interface IStockPriceSource
{
    /// <summary>Short provider name used for logging and diagnostics (e.g. "AlphaVantage").</summary>
    string Name { get; }
    MarketDataProvider? Provider => null;
    int Priority => int.MaxValue;
    IReadOnlySet<AssetType> SupportedAssetTypes => new HashSet<AssetType>();
    IReadOnlySet<string> SupportedIntervals => new HashSet<string>();
    bool SupportsProviderSelection => false;

    /// <summary>
    /// Fetches the daily close series for <paramref name="symbol"/> between
    /// <paramref name="start"/> and <paramref name="end"/> (inclusive). Returns an empty list
    /// when the provider has no data, is unconfigured, or fails — never throws for those cases,
    /// so a fallback source can take over.
    /// </summary>
    /// <param name="symbol">The provider-recognised symbol (e.g. "AAPL", "CSPX.LON").</param>
    /// <param name="isin">ISIN to stamp on the returned prices; the canonical key downstream.</param>
    Task<IReadOnlyList<StockPrice>> GetDailySeries(string symbol, string isin, DateTime start, DateTime end, Currency currency, CancellationToken ct = default);

    Task<IReadOnlyList<StockPrice>> GetDailySeries(
        MarketDataProvider provider,
        string symbol,
        string isin,
        DateTime start,
        DateTime end,
        Currency currency,
        CancellationToken ct = default) =>
        Provider == provider ? GetDailySeries(symbol, isin, start, end, currency, ct) : Task.FromResult<IReadOnlyList<StockPrice>>([]);

    async Task<StockPrice?> GetLatestQuote(
        string symbol,
        string isin,
        Currency currency,
        CancellationToken ct = default)
    {
        var end = DateTime.UtcNow;
        var prices = await GetDailySeries(symbol, isin, end.AddDays(-7), end, currency, ct);
        return prices.MaxBy(x => x.Date);
    }

    int GetPriority(MarketDataProvider provider) => Provider == provider ? Priority : int.MaxValue;
}