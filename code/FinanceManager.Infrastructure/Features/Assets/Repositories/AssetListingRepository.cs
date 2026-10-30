using FinanceManager.Domain.Assets.Entities;
using FinanceManager.Domain.Assets.Repositories;
using FinanceManager.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FinanceManager.Infrastructure.Features.Assets.Repositories;

public class AssetListingRepository(AppDbContext context) : IAssetListingRepository
{
    public Task<AssetListing?> Get(long id, CancellationToken cancellationToken = default) =>
        context.AssetListings
            .Include(x => x.MarketDataSymbols)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<AssetListing?> Get(string ticker, string exchangeMic, string tradingCurrency, CancellationToken cancellationToken = default) =>
        context.AssetListings
            .Include(x => x.MarketDataSymbols)
            .FirstOrDefaultAsync(
                x => x.Ticker == ticker && x.ExchangeMic == exchangeMic && x.TradingCurrency == tradingCurrency,
                cancellationToken);

    public async Task<IReadOnlyList<AssetListing>> GetByAsset(long assetId, CancellationToken cancellationToken = default) =>
        await context.AssetListings.AsNoTracking().Where(x => x.AssetId == assetId).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<AssetListing>> SearchAsync(string query, int maxResults, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query)) return [];
        var trimmed = query.Trim();
        var escapedQuery = trimmed.ToUpperInvariant()
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("%", "\\%", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal);
        var tickerPattern = $"{escapedQuery}%";
        var likePattern = $"%{escapedQuery}%";
        return await context.AssetListings
            .AsNoTracking()
            .Include(x => x.Asset)
            .Include(x => x.MarketDataSymbols)
            .Where(x => x.IsActive && (EF.Functions.Like(x.Ticker.ToUpper(), tickerPattern, "\\")
                || EF.Functions.Like(x.ExchangeName.ToUpper(), likePattern, "\\")
                || EF.Functions.Like(x.Asset.Name.ToUpper(), likePattern, "\\")
                || (x.Asset.Isin != null && EF.Functions.Like(x.Asset.Isin.ToUpper(), likePattern, "\\"))))
            .OrderByDescending(x => x.IsPrimaryListing)
            .ThenBy(x => x.Ticker)
            .Take(maxResults)
            .ToListAsync(cancellationToken);
    }

    public async Task<AssetListing> Add(AssetListing listing, CancellationToken cancellationToken = default)
    {
        EnsureValidPriceMultiplier(listing);
        var now = DateTimeOffset.UtcNow;
        if (listing.CreatedAt == default) listing.CreatedAt = now;
        listing.UpdatedAt = now;

        context.AssetListings.Add(listing);
        await context.SaveChangesAsync(cancellationToken);
        return listing;
    }

    public async Task<AssetListing> Upsert(AssetListing listing, CancellationToken cancellationToken = default)
    {
        EnsureValidPriceMultiplier(listing);
        var existing = await FindExistingForUpsertAsync(listing, cancellationToken);

        if (existing is not null)
        {
            existing.AssetId = listing.AssetId;
            existing.ExchangeMic ??= listing.ExchangeMic;
            if (!string.IsNullOrWhiteSpace(listing.ExchangeName))
                existing.ExchangeName = listing.ExchangeName;
            existing.TradingCurrency ??= listing.TradingCurrency;
            existing.ListingFigi ??= listing.ListingFigi;
            existing.ExchangeInstrumentId ??= listing.ExchangeInstrumentId;
            existing.IsPrimaryListing = listing.IsPrimaryListing;
            existing.PriceMultiplier = listing.PriceMultiplier;
            existing.IsActive = listing.IsActive;
            existing.UpdatedAt = DateTimeOffset.UtcNow;
            await context.SaveChangesAsync(cancellationToken);
            return existing;
        }

        try
        {
            return await Add(listing, cancellationToken);
        }
        catch (DbUpdateException)
        {
            context.Entry(listing).State = EntityState.Detached;
            var concurrent = await FindExistingForUpsertAsync(listing, cancellationToken);
            if (concurrent is null) throw;
            return concurrent;
        }
    }

    private async Task<AssetListing?> FindExistingForUpsertAsync(AssetListing listing, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(listing.ExchangeMic) && !string.IsNullOrWhiteSpace(listing.TradingCurrency))
        {
            var byCompleteKey = await context.AssetListings.FirstOrDefaultAsync(
                x => x.Ticker == listing.Ticker
                    && x.ExchangeMic == listing.ExchangeMic
                    && x.TradingCurrency == listing.TradingCurrency,
                cancellationToken);
            if (byCompleteKey is not null)
                return byCompleteKey;
        }

        if (!string.IsNullOrWhiteSpace(listing.ListingFigi))
        {
            var byFigi = await context.AssetListings.FirstOrDefaultAsync(
                x => x.ListingFigi == listing.ListingFigi,
                cancellationToken);
            if (byFigi is not null)
                return byFigi;
        }

        return await context.AssetListings.FirstOrDefaultAsync(
            x => x.AssetId == listing.AssetId
                && x.Ticker == listing.Ticker
                && x.ExchangeMic == listing.ExchangeMic
                && x.TradingCurrency == listing.TradingCurrency
                && x.ExchangeName == listing.ExchangeName,
            cancellationToken);
    }

    public async Task<bool> Update(AssetListing listing, CancellationToken cancellationToken = default)
    {
        EnsureValidPriceMultiplier(listing);

        var existing = await context.AssetListings.FirstOrDefaultAsync(x => x.Id == listing.Id, cancellationToken);
        if (existing is null) return false;

        existing.Ticker = listing.Ticker;
        existing.ExchangeMic = listing.ExchangeMic;
        existing.ExchangeName = listing.ExchangeName;
        existing.TradingCurrency = listing.TradingCurrency;
        existing.ListingFigi = listing.ListingFigi;
        existing.ExchangeInstrumentId = listing.ExchangeInstrumentId;
        existing.IsPrimaryListing = listing.IsPrimaryListing;
        existing.PriceMultiplier = listing.PriceMultiplier;
        existing.IsActive = listing.IsActive;
        existing.UpdatedAt = DateTimeOffset.UtcNow;
        await context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> Delete(long id, CancellationToken cancellationToken = default)
    {
        var entity = await context.AssetListings.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity is null) return false;

        context.AssetListings.Remove(entity);
        await context.SaveChangesAsync(cancellationToken);
        return true;
    }

    // A non-positive multiplier would yield zero/negative normalized prices in valuation flows.
    private static void EnsureValidPriceMultiplier(AssetListing listing)
    {
        if (listing.PriceMultiplier <= 0m)
            throw new ArgumentOutOfRangeException(nameof(listing), listing.PriceMultiplier, "AssetListing.PriceMultiplier must be greater than zero.");
    }
}