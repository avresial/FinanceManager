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
    /// Get the normalised price per unit for <paramref name="assetListingId"/> converted to
    /// <paramref name="targetCurrency"/> as of <paramref name="asOf"/>. Uses the most recent quote
    /// on or before that date, fetching from the provider when the cache has nothing.
    /// Returns 0 when no price can be determined.
    /// </summary>
    Task<decimal> GetPricePerUnitAsync(long assetListingId, Currency targetCurrency, DateTime asOf, CancellationToken ct = default);

    /// <summary>
    /// Get the per-day price per unit for <paramref name="assetListingId"/> converted to
    /// <paramref name="targetCurrency"/> over [<paramref name="start"/>, <paramref name="end"/>].
    /// Days without a quote carry the latest known older price forward.
    /// </summary>
    Task<IReadOnlyDictionary<DateTime, decimal>> GetPricePerUnitSeriesAsync(
        long assetListingId,
        Currency targetCurrency,
        DateTime start,
        DateTime end,
        CancellationToken ct = default);
}