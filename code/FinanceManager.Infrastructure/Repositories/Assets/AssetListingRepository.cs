using FinanceManager.Domain.Assets.Entities;
using FinanceManager.Domain.Assets.Repositories;
using FinanceManager.Infrastructure.Contexts;
using Microsoft.EntityFrameworkCore;

namespace FinanceManager.Infrastructure.Repositories.Assets;

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

    public async Task<IReadOnlyList<AssetListing>> GetByTicker(string ticker, CancellationToken cancellationToken = default) =>
        await context.AssetListings.AsNoTracking().Where(x => x.Ticker == ticker).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<AssetListing>> GetByAsset(long assetId, CancellationToken cancellationToken = default) =>
        await context.AssetListings.AsNoTracking().Where(x => x.AssetId == assetId).ToListAsync(cancellationToken);

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
        var existing = await context.AssetListings.FirstOrDefaultAsync(
            x => x.Ticker == listing.Ticker && x.ExchangeMic == listing.ExchangeMic && x.TradingCurrency == listing.TradingCurrency,
            cancellationToken);

        if (existing is not null)
        {
            existing.AssetId = listing.AssetId;
            existing.ExchangeName = listing.ExchangeName;
            existing.ListingFigi = listing.ListingFigi;
            existing.ExchangeInstrumentId = listing.ExchangeInstrumentId;
            existing.IsPrimaryListing = listing.IsPrimaryListing;
            existing.PriceMultiplier = listing.PriceMultiplier;
            existing.IsActive = listing.IsActive;
            existing.UpdatedAt = DateTimeOffset.UtcNow;
            await context.SaveChangesAsync(cancellationToken);
            return existing;
        }

        return await Add(listing, cancellationToken);
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