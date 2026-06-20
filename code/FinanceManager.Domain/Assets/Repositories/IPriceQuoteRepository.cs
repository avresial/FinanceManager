using FinanceManager.Domain.Assets.Entities;

namespace FinanceManager.Domain.Assets.Repositories;

public interface IPriceQuoteRepository
{
    Task<PriceQuote?> Get(long id, CancellationToken cancellationToken = default);

    /// <summary>The most recent quote on or before <paramref name="asOf"/> for a listing, optionally restricted to a provider.</summary>
    Task<PriceQuote?> GetLatestOnOrBefore(long assetListingId, DateTimeOffset asOf, MarketDataProvider? provider = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PriceQuote>> GetRange(long assetListingId, DateTimeOffset start, DateTimeOffset end, CancellationToken cancellationToken = default);
    Task<PriceQuote> Add(PriceQuote quote, CancellationToken cancellationToken = default);

    /// <summary>Insert the quote, or update the existing one matched by (AssetListingId, Provider, PriceTime, QuoteType).</summary>
    Task<PriceQuote> Upsert(PriceQuote quote, CancellationToken cancellationToken = default);
    Task<bool> Delete(long id, CancellationToken cancellationToken = default);
}