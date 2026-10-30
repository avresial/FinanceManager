using FinanceManager.Domain.Assets.Dtos;
using FinanceManager.Domain.Assets.Entities;
using FinanceManager.Domain.Assets.Repositories;
using FinanceManager.Domain.FinancialAccounts.Investments.Entities;
using FinanceManager.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FinanceManager.Infrastructure.Features.Assets.Repositories;

public class AssetRepository(AppDbContext context) : IAssetRepository
{
    public Task<Asset?> Get(long id, CancellationToken cancellationToken = default) =>
        context.Assets
            .Include(x => x.Listings).ThenInclude(l => l.MarketDataSymbols)
            .Include(x => x.Identifiers)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<Asset?> GetByIsin(string isin, CancellationToken cancellationToken = default) =>
        context.Assets
            .Include(x => x.Listings)
            .Include(x => x.Identifiers)
            .FirstOrDefaultAsync(x => x.Isin == isin, cancellationToken);

    public Task<Asset?> GetByShareClassFigi(string shareClassFigi, CancellationToken cancellationToken = default) =>
        context.Assets
            .Include(x => x.Listings)
            .Include(x => x.Identifiers)
            .FirstOrDefaultAsync(x => x.ShareClassFigi == shareClassFigi, cancellationToken);

    public async Task<IReadOnlyList<Asset>> GetAll(CancellationToken cancellationToken = default) =>
        await context.Assets.AsNoTracking().OrderBy(x => x.Name).ToListAsync(cancellationToken);

    public async Task<IncompleteAssetPage> GetIncomplete(int page, int pageSize, string? search, DateTimeOffset staleBefore, CancellationToken cancellationToken = default)
    {
        var today = DateOnly.FromDateTime(staleBefore.AddDays(7).UtcDateTime);
        var query = context.Assets.AsNoTracking()
            .SelectMany(asset => asset.Listings.DefaultIfEmpty(), (asset, listing) => new { Asset = asset, Listing = listing })
            .Where(x => x.Listing == null
                || string.IsNullOrWhiteSpace(x.Listing.ExchangeMic)
                || string.IsNullOrWhiteSpace(x.Listing.TradingCurrency)
                || x.Asset.Type == AssetType.Other
                || (string.IsNullOrWhiteSpace(x.Asset.Isin) && string.IsNullOrWhiteSpace(x.Asset.ShareClassFigi) && string.IsNullOrWhiteSpace(x.Asset.CompositeFigi))
                || !x.Listing.MarketDataSymbols.Any(s => s.IsEnabled)
                || !x.Listing.MarketDataSymbols.Any(s => s.IsEnabled && string.IsNullOrEmpty(s.LastError))
                || !x.Listing.MarketDataSymbols.Any(s => s.IsEnabled && s.LastSuccessfulPriceFetchAt >= staleBefore));
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToUpperInvariant();
            query = query.Where(x => x.Asset.Name.ToUpper().Contains(term)
                || (x.Asset.Isin != null && x.Asset.Isin.ToUpper().Contains(term))
                || (x.Listing != null && x.Listing.Ticker.ToUpper().Contains(term)));
        }
        var count = await query.CountAsync(cancellationToken);
        var items = await query.OrderBy(x => x.Asset.Name).ThenBy(x => x.Asset.Id).ThenBy(x => x.Listing == null ? 0 : x.Listing.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new IncompleteAssetItem(
                x.Asset.Id, x.Asset.Name, x.Listing == null ? null : x.Listing.Id,
                x.Listing == null ? null : x.Listing.Ticker,
                x.Listing == null ? null : x.Listing.ExchangeMic,
                x.Listing == null ? null : x.Listing.TradingCurrency,
                x.Listing == null ? 0 : context.InvestmentTransactions
                    .Where(t => t.AssetListingId == x.Listing.Id && t.TradeDate <= today)
                    .GroupBy(t => t.UserId)
                    .Where(g => g.Sum(t => t.Type == InvestmentTransactionType.Buy ? t.Quantity : -t.Quantity) > 0).Count(),
                x.Listing == null ? null : x.Listing.MarketDataSymbols.Where(s => s.IsEnabled && s.LastError != null && s.LastError != "")
                    .OrderByDescending(s => s.UpdatedAt).ThenBy(s => s.Id).Select(s => s.LastError).FirstOrDefault(),
                x.Listing != null && string.IsNullOrWhiteSpace(x.Listing.ExchangeMic),
                x.Listing != null && string.IsNullOrWhiteSpace(x.Listing.TradingCurrency),
                x.Asset.Type == AssetType.Other,
                string.IsNullOrWhiteSpace(x.Asset.Isin) && string.IsNullOrWhiteSpace(x.Asset.ShareClassFigi) && string.IsNullOrWhiteSpace(x.Asset.CompositeFigi),
                x.Listing != null && !x.Listing.MarketDataSymbols.Any(s => s.IsEnabled),
                x.Listing != null && x.Listing.MarketDataSymbols.Any(s => s.IsEnabled) && !x.Listing.MarketDataSymbols.Any(s => s.IsEnabled && string.IsNullOrEmpty(s.LastError)),
                x.Listing != null && x.Listing.MarketDataSymbols.Any(s => s.IsEnabled) && !x.Listing.MarketDataSymbols.Any(s => s.IsEnabled && s.LastSuccessfulPriceFetchAt >= staleBefore)))
            .ToListAsync(cancellationToken);
        return new(items, count, page, pageSize);
    }

    public async Task<Asset> Add(Asset asset, CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        if (asset.CreatedAt == default) asset.CreatedAt = now;
        asset.UpdatedAt = now;

        context.Assets.Add(asset);
        await context.SaveChangesAsync(cancellationToken);
        return asset;
    }

    public async Task<Asset> Upsert(Asset asset, CancellationToken cancellationToken = default)
    {
        // Identity is FIGI-centric: prefer matching an existing asset by ISIN when one is supplied,
        // otherwise fall back to the canonical share-class FIGI. Either key locates the same security.
        var existing = await FindByIdentityAsync(asset, cancellationToken);

        if (existing is not null)
        {
            existing.Name = asset.Name;
            existing.Type = asset.Type;
            // Backfill the ISIN cross-reference when it was previously unknown; never clear a known one.
            if (!string.IsNullOrWhiteSpace(asset.Isin))
                existing.Isin = asset.Isin;
            // Likewise never clear the canonical share-class FIGI when the incoming asset omits it.
            if (!string.IsNullOrWhiteSpace(asset.ShareClassFigi))
                existing.ShareClassFigi = asset.ShareClassFigi;
            existing.CompositeFigi = asset.CompositeFigi;
            existing.Issuer = asset.Issuer;
            existing.Domicile = asset.Domicile;
            existing.BaseCurrency = asset.BaseCurrency;
            existing.DistributionPolicy = asset.DistributionPolicy;
            existing.BenchmarkIndex = asset.BenchmarkIndex;
            existing.ReplicationMethod = asset.ReplicationMethod;
            existing.TotalExpenseRatio = asset.TotalExpenseRatio;
            existing.IsUcits = asset.IsUcits;
            existing.InceptionDate = asset.InceptionDate;
            existing.UpdatedAt = DateTimeOffset.UtcNow;
            await context.SaveChangesAsync(cancellationToken);
            return existing;
        }

        try
        {
            return await Add(asset, cancellationToken);
        }
        catch (DbUpdateException)
        {
            context.Entry(asset).State = EntityState.Detached;
            existing = await FindByIdentityAsync(asset, cancellationToken);
            if (existing is null) throw;
            return existing;
        }
    }

    private async Task<Asset?> FindByIdentityAsync(Asset asset, CancellationToken cancellationToken)
    {
        Asset? existing = null;
        if (!string.IsNullOrWhiteSpace(asset.Isin))
            existing = await context.Assets.FirstOrDefaultAsync(x => x.Isin == asset.Isin, cancellationToken);

        if (existing is null && !string.IsNullOrWhiteSpace(asset.ShareClassFigi))
            existing = await context.Assets.FirstOrDefaultAsync(x => x.ShareClassFigi == asset.ShareClassFigi, cancellationToken);

        return existing;
    }

    public async Task<bool> Update(Asset asset, CancellationToken cancellationToken = default)
    {
        // Copy scalars onto the tracked row so CreatedAt and the listings graph are left untouched.
        var existing = await context.Assets.FirstOrDefaultAsync(x => x.Id == asset.Id, cancellationToken);
        if (existing is null) return false;

        existing.Name = asset.Name;
        existing.Type = asset.Type;
        existing.Isin = asset.Isin;
        existing.ShareClassFigi = asset.ShareClassFigi;
        existing.CompositeFigi = asset.CompositeFigi;
        existing.Issuer = asset.Issuer;
        existing.Domicile = asset.Domicile;
        existing.BaseCurrency = asset.BaseCurrency;
        existing.DistributionPolicy = asset.DistributionPolicy;
        existing.BenchmarkIndex = asset.BenchmarkIndex;
        existing.ReplicationMethod = asset.ReplicationMethod;
        existing.TotalExpenseRatio = asset.TotalExpenseRatio;
        existing.IsUcits = asset.IsUcits;
        existing.InceptionDate = asset.InceptionDate;
        existing.UpdatedAt = DateTimeOffset.UtcNow;
        await context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> Delete(long id, CancellationToken cancellationToken = default)
    {
        var entity = await context.Assets.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity is null) return false;

        context.Assets.Remove(entity);
        await context.SaveChangesAsync(cancellationToken);
        return true;
    }
}