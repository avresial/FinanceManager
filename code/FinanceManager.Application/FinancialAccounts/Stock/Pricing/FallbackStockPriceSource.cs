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

    public async Task<IReadOnlyList<StockPrice>> GetDailySeries(string symbol, string isin, DateTime start, DateTime end, Currency currency, CancellationToken ct = default)
    {
        for (var i = 0; i < sources.Count; i++)
        {
            var source = sources[i];
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

    // Strip CR/LF so an attacker-influenced symbol cannot forge log entries (log injection).
    private static string Sanitize(string value)
        => value.Replace("\r", string.Empty).Replace("\n", string.Empty);
}