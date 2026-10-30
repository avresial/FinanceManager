using FinanceManager.Domain.Assets.Dtos;
using FinanceManager.Domain.Assets.Entities;
using FinanceManager.Domain.Assets.Repositories;

namespace FinanceManager.Application.FinancialAccounts.Investments.Discovery;

public sealed class IncompleteAssetQueueService(
    IAssetRepository assets, IAssetListingRepository listings,
    IMarketDataSymbolRepository symbols, IEnumerable<IMarketDataSymbolResolver> resolvers)
{
    public Task<IncompleteAssetPage> GetAsync(int page, int pageSize, string? search, CancellationToken ct = default)
    {
        if (page < 1 || pageSize is < 1 or > 50 || page > int.MaxValue / pageSize)
            throw new ArgumentException("Page must be positive and page size must be between 1 and 50.");
        if (search?.Length > 200) throw new ArgumentException("Search must be 200 characters or fewer.");
        return assets.GetIncomplete(page, pageSize, search, DateTimeOffset.UtcNow.AddDays(-7), ct);
    }

    public async Task<MarketDataSymbolDto> ValidateAndSaveSymbolAsync(long listingId, MarketDataSymbolDto draft, CancellationToken ct = default)
    {
        var listing = await listings.Get(listingId, ct) ?? throw new KeyNotFoundException("Listing not found.");
        if (string.IsNullOrWhiteSpace(listing.ExchangeMic) || string.IsNullOrWhiteSpace(listing.TradingCurrency))
            throw new ArgumentException("Save the listing's venue and trading currency before validating symbols.");
        if (string.IsNullOrWhiteSpace(draft.Symbol) || draft.Symbol.Length > 200)
            throw new ArgumentException("Enter a provider symbol of at most 200 characters.");
        var resolver = resolvers.FirstOrDefault(x => x.Provider == draft.Provider)
            ?? throw new ArgumentException("This provider does not support recent-price validation.");
        var existing = await symbols.Get(draft.Provider, draft.Symbol.Trim(), ct);
        if (existing is not null && existing.AssetListingId != listingId)
            throw new ArgumentException("This provider symbol belongs to another listing. Review it in the full editor.");
        var candidate = new Domain.Assets.Discovery.MarketDataSymbolResolution(draft.Provider, resolver.Priority,
            "candidate", draft.Symbol.Trim(), draft.ProviderExchangeCode, listing.TradingCurrency);
        bool valid;
        try { valid = await resolver.ValidateAsync(candidate, ct); }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception) { throw new ArgumentException("Provider validation failed or timed out. Nothing was saved; retry later."); }
        if (!valid) throw new ArgumentException("No positive recent price returned. Nothing was saved; check the symbol or retry later.");
        var entity = existing ?? new MarketDataSymbol { AssetListingId = listingId, Provider = draft.Provider, Symbol = candidate.Symbol! };
        entity.IsEnabled = true;
        entity.Currency = listing.TradingCurrency;
        entity.ProviderExchangeCode = draft.ProviderExchangeCode;
        entity.IsPrimary = (existing is null || existing.IsPrimary)
            && !listing.MarketDataSymbols.Any(x => x.Id != entity.Id && x.IsPrimary && x.IsEnabled);
        entity.LastValidatedAt = DateTimeOffset.UtcNow;
        entity.LastSuccessfulPriceFetchAt = entity.LastValidatedAt;
        entity.LastError = null;
        if (existing is null) await symbols.Add(entity, ct);
        else
        {
            if (!await symbols.Update(entity, ct) || !await symbols.RecordFetchResult(entity.Id, entity.LastValidatedAt, null, ct))
                throw new KeyNotFoundException("The symbol was removed during validation. Refresh the queue and retry.");
        }
        return entity.ToDto();
    }
}