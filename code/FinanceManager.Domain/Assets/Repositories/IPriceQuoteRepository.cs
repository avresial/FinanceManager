using FinanceManager.Domain.Assets.Entities;

namespace FinanceManager.Domain.Assets.Repositories;

public interface IPriceQuoteRepository
{
    /// <summary>Get a price quote by its database id, or <c>null</c> if none exists.</summary>
    Task<PriceQuote?> Get(long id, CancellationToken cancellationToken = default);

    /// <summary>The most recent quote on or before <paramref name="asOf"/> for a listing, optionally restricted to a provider.</summary>
    Task<PriceQuote?> GetLatestOnOrBefore(long assetListingId, DateTimeOffset asOf, MarketDataProvider? provider = null, CancellationToken cancellationToken = default);

    /// <summary>All quotes for a listing whose <see cref="PriceQuote.PriceTime"/> falls within [start, end].</summary>
    Task<IReadOnlyList<PriceQuote>> GetRange(long assetListingId, DateTimeOffset start, DateTimeOffset end, CancellationToken cancellationToken = default);

    /// <summary>Insert a new price quote and return the persisted entity (with its generated id).</summary>
    Task<PriceQuote> Add(PriceQuote quote, CancellationToken cancellationToken = default);

    /// <summary>Insert the quote, or update the existing one matched by (AssetListingId, Provider, PriceTime, QuoteType).</summary>
    Task<PriceQuote> Upsert(PriceQuote quote, CancellationToken cancellationToken = default);

    /// <summary>Delete the quote with the given id. Returns <c>false</c> when no such quote exists.</summary>
    Task<bool> Delete(long id, CancellationToken cancellationToken = default);
}