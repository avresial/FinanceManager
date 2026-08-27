using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;

namespace FinanceManager.Domain.Assets.Services;

/// <summary>
/// Reads prices for an <see cref="Entities.AssetListing"/> with automatic provider fallback:
/// serves cached <see cref="Entities.PriceQuote"/> rows first, fetches from the listing's
/// configured <see cref="Entities.MarketDataSymbol"/> when data is missing, normalises the raw
/// quote with <see cref="Entities.AssetListing.PriceMultiplier"/> (e.g. GBX → GBP) and converts
/// to the requested currency. Keyed on the listing rather than a globally-unique ticker, so the
/// same instrument trading on several exchanges is priced independently.
/// </summary>
public interface IInvestmentPriceProvider
{
    /// <summary>
    /// Resolves a price while preserving a safe retry message when today's UTC exchange rate has
    /// not been published yet.
    /// </summary>
    Task<InvestmentPriceResult> GetPricePerUnitResultAsync(
        long assetListingId,
        Currency targetCurrency,
        DateTime asOf,
        CancellationToken ct = default);

    /// <summary>
    /// Get the normalised price per unit for <paramref name="assetListingId"/> converted to
    /// <paramref name="targetCurrency"/> as of <paramref name="asOf"/>. Uses the most recent quote
    /// on or before that date, fetching from the provider when the cache has nothing. A weekend
    /// <paramref name="asOf"/> is priced from that week's Friday close — exchanges are shut, so no
    /// provider publishes a Saturday or Sunday value. Returns 0 when no price can be determined.
    /// </summary>
    Task<decimal> GetPricePerUnitAsync(long assetListingId, Currency targetCurrency, DateTime asOf, CancellationToken ct = default);

    /// <summary>
    /// Get the per-day price per unit for <paramref name="assetListingId"/> converted to
    /// <paramref name="targetCurrency"/> over [<paramref name="start"/>, <paramref name="end"/>].
    /// Days without a quote — weekends included — carry the latest known older price forward, so a
    /// Saturday and Sunday both report that week's Friday close.
    /// </summary>
    Task<IReadOnlyDictionary<DateTime, decimal>> GetPricePerUnitSeriesAsync(
        long assetListingId,
        Currency targetCurrency,
        DateTime start,
        DateTime end,
        CancellationToken ct = default);

    /// <summary>
    /// Ensure stored end-of-day quotes exist for <paramref name="assetListingId"/> over
    /// [<paramref name="start"/>, <paramref name="end"/>], fetching from the provider chain and
    /// persisting when coverage is missing. Performs no currency conversion. Only trading days are
    /// requested from the provider; a range holding no trading day at all is answered from the last
    /// close before it. Returns <c>true</c> when at least one quote covers the range after the attempt.
    /// </summary>
    Task<bool> EnsureQuotesAsync(long assetListingId, DateTime start, DateTime end, CancellationToken ct = default);
}