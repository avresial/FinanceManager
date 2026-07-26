using FinanceManager.Domain.Shared.Maintenance.Entities;
using FinanceManager.Domain.Shared.Maintenance.Repositories;
using FinanceManager.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FinanceManager.Infrastructure.Features.Administration.Repositories;

internal sealed class MaintenanceKeyRepository(AppDbContext dbContext) : IMaintenanceKeyRepository
{
    public Task<MaintenanceApiKey?> Get(CancellationToken cancellationToken = default) =>
        dbContext.MaintenanceApiKeys.AsNoTracking().FirstOrDefaultAsync(cancellationToken);

    public async Task Save(MaintenanceApiKey key, CancellationToken cancellationToken = default)
    {
        // Only one key may be active: rolling replaces the row rather than accumulating history.
        var existing = await dbContext.MaintenanceApiKeys.ToListAsync(cancellationToken);
        dbContext.MaintenanceApiKeys.RemoveRange(existing);
        dbContext.MaintenanceApiKeys.Add(key);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> Delete(CancellationToken cancellationToken = default)
    {
        var existing = await dbContext.MaintenanceApiKeys.ToListAsync(cancellationToken);
        if (existing.Count == 0) return false;

        dbContext.MaintenanceApiKeys.RemoveRange(existing);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }
}