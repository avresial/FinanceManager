using FinanceManager.Domain.Assets.Entities;

namespace FinanceManager.Domain.Assets.Repositories;

public interface IAssetListingRepository
{
    Task<AssetListing?> Get(long id, CancellationToken cancellationToken = default);
    Task<AssetListing?> Get(string ticker, string exchangeMic, string tradingCurrency, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AssetListing>> GetByTicker(string ticker, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AssetListing>> GetByAsset(long assetId, CancellationToken cancellationToken = default);
    Task<AssetListing> Add(AssetListing listing, CancellationToken cancellationToken = default);

    /// <summary>Insert the listing, or update the existing one matched by (Ticker, ExchangeMic, TradingCurrency).</summary>
    Task<AssetListing> Upsert(AssetListing listing, CancellationToken cancellationToken = default);
    Task<bool> Delete(long id, CancellationToken cancellationToken = default);
}