using FinanceManager.Domain.Assets.Entities;
using FinanceManager.Domain.Assets.Repositories;
using FinanceManager.Infrastructure.Contexts;
using Microsoft.EntityFrameworkCore;

namespace FinanceManager.Infrastructure.Repositories.Assets;

public class AssetRepository(AppDbContext context) : IAssetRepository
{
    public Task<Asset?> Get(long id, CancellationToken cancellationToken = default) =>
        context.Assets
            .Include(x => x.Listings)
            .Include(x => x.Identifiers)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<Asset?> GetByIsin(string isin, CancellationToken cancellationToken = default) =>
        context.Assets
            .Include(x => x.Listings)
            .Include(x => x.Identifiers)
            .FirstOrDefaultAsync(x => x.Isin == isin, cancellationToken);

    public async Task<IReadOnlyList<Asset>> GetAll(CancellationToken cancellationToken = default) =>
        await context.Assets.AsNoTracking().OrderBy(x => x.Name).ToListAsync(cancellationToken);

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
        if (!string.IsNullOrWhiteSpace(asset.Isin))
        {
            var existing = await context.Assets.FirstOrDefaultAsync(x => x.Isin == asset.Isin, cancellationToken);
            if (existing is not null)
            {
                existing.Name = asset.Name;
                existing.Type = asset.Type;
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
        }

        return await Add(asset, cancellationToken);
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