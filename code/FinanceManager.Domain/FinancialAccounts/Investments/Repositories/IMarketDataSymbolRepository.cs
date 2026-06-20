using FinanceManager.Domain.FinancialAccounts.Investments.Entities;

namespace FinanceManager.Domain.FinancialAccounts.Investments.Repositories;

public interface IMarketDataSymbolRepository
{
    Task<MarketDataSymbol?> Get(long id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MarketDataSymbol>> GetByListing(long assetListingId, CancellationToken cancellationToken = default);

    /// <summary>The preferred enabled symbol for a listing and provider (IsPrimary first, then any enabled).</summary>
    Task<MarketDataSymbol?> GetPrimary(long assetListingId, MarketDataProvider provider, CancellationToken cancellationToken = default);
    Task<MarketDataSymbol> Add(MarketDataSymbol symbol, CancellationToken cancellationToken = default);

    /// <summary>Insert the symbol, or update the existing one matched by (Provider, Symbol).</summary>
    Task<MarketDataSymbol> Upsert(MarketDataSymbol symbol, CancellationToken cancellationToken = default);

    /// <summary>Record the outcome of a price fetch (success timestamp and/or last error) for diagnostics.</summary>
    Task RecordFetchResult(long id, DateTimeOffset? lastSuccessfulPriceFetchAt, string? lastError, CancellationToken cancellationToken = default);
    Task<bool> Delete(long id, CancellationToken cancellationToken = default);
}