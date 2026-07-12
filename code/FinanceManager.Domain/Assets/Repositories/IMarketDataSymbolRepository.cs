using FinanceManager.Domain.Assets.Entities;

namespace FinanceManager.Domain.Assets.Repositories;

public interface IMarketDataSymbolRepository
{
    /// <summary>Get a market-data symbol by its database id, or <c>null</c> if none exists.</summary>
    Task<MarketDataSymbol?> Get(long id, CancellationToken cancellationToken = default);

    /// <summary>Get all provider symbols mapped to the given listing.</summary>
    Task<IReadOnlyList<MarketDataSymbol>> GetByListing(long assetListingId, CancellationToken cancellationToken = default);

    /// <summary>The preferred enabled symbol for a listing and provider (IsPrimary first, then any enabled).</summary>
    Task<MarketDataSymbol?> GetPrimary(long assetListingId, MarketDataProvider provider, CancellationToken cancellationToken = default);

    /// <summary>Get the globally unique provider symbol mapping, or <c>null</c>.</summary>
    Task<MarketDataSymbol?> Get(MarketDataProvider provider, string symbol, CancellationToken cancellationToken = default);

    /// <summary>Insert a new provider symbol and return the persisted entity (with its generated id).</summary>
    Task<MarketDataSymbol> Add(MarketDataSymbol symbol, CancellationToken cancellationToken = default);

    /// <summary>Insert the symbol, or update the existing one matched by (Provider, Symbol).</summary>
    Task<MarketDataSymbol> Upsert(MarketDataSymbol symbol, CancellationToken cancellationToken = default);

    /// <summary>Update an existing symbol's editable fields by id (AssetListingId and CreatedAt untouched). Returns <c>false</c> when none exists.</summary>
    Task<bool> Update(MarketDataSymbol symbol, CancellationToken cancellationToken = default);

    /// <summary>
    /// Record the outcome of a price fetch (success timestamp and/or last error) for diagnostics.
    /// Returns <c>false</c> when no symbol with the given id exists.
    /// </summary>
    Task<bool> RecordFetchResult(long id, DateTimeOffset? lastSuccessfulPriceFetchAt, string? lastError, CancellationToken cancellationToken = default);

    /// <summary>Delete the symbol with the given id. Returns <c>false</c> when no such symbol exists.</summary>
    Task<bool> Delete(long id, CancellationToken cancellationToken = default);
}