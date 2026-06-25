using FinanceManager.Domain.Assets.Entities;

namespace FinanceManager.Domain.Assets.Repositories;

public interface IAssetListingRepository
{
    /// <summary>Get a listing by its database id (including its market-data symbols), or <c>null</c> if none exists.</summary>
    Task<AssetListing?> Get(long id, CancellationToken cancellationToken = default);

    /// <summary>Get the listing uniquely identified by ticker, exchange MIC and trading currency, or <c>null</c>.</summary>
    Task<AssetListing?> Get(string ticker, string exchangeMic, string tradingCurrency, CancellationToken cancellationToken = default);

    /// <summary>Get all listings that share the given ticker across exchanges (tickers are not globally unique).</summary>
    Task<IReadOnlyList<AssetListing>> GetByTicker(string ticker, CancellationToken cancellationToken = default);

    /// <summary>Get all listings belonging to the given asset.</summary>
    Task<IReadOnlyList<AssetListing>> GetByAsset(long assetId, CancellationToken cancellationToken = default);

    /// <summary>Search active listings whose ticker starts with <paramref name="query"/> or whose exchange name contains it. Returns at most <paramref name="maxResults"/> results ordered by primary listing first, then ticker.</summary>
    Task<IReadOnlyList<AssetListing>> SearchAsync(string query, int maxResults, CancellationToken cancellationToken = default);

    /// <summary>Insert a new listing and return the persisted entity (with its generated id).</summary>
    Task<AssetListing> Add(AssetListing listing, CancellationToken cancellationToken = default);

    /// <summary>Insert the listing, or update the existing one matched by (Ticker, ExchangeMic, TradingCurrency).</summary>
    Task<AssetListing> Upsert(AssetListing listing, CancellationToken cancellationToken = default);

    /// <summary>Update an existing listing's editable fields by id (AssetId and CreatedAt untouched). Returns <c>false</c> when none exists.</summary>
    Task<bool> Update(AssetListing listing, CancellationToken cancellationToken = default);

    /// <summary>Delete the listing with the given id. Returns <c>false</c> when no such listing exists.</summary>
    Task<bool> Delete(long id, CancellationToken cancellationToken = default);
}