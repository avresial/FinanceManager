using FinanceManager.Domain.Entities.ExternalServices;
using FinanceManager.Domain.Repositories;
using FinanceManager.Infrastructure.Contexts;
using Microsoft.EntityFrameworkCore;

namespace FinanceManager.Infrastructure.Repositories;

internal sealed class ExternalServiceConfigRepository(AppDbContext dbContext) : IExternalServiceConfigRepository
{
    public Task<List<ExternalServiceConfiguration>> GetAllAsync(CancellationToken ct = default) =>
        dbContext.ExternalServiceConfigurations.AsNoTracking().ToListAsync(ct);

    public Task<ExternalServiceConfiguration?> GetByNameAsync(string serviceName, CancellationToken ct = default) =>
        dbContext.ExternalServiceConfigurations.AsNoTracking()
            .FirstOrDefaultAsync(s => s.ServiceName == serviceName, ct);

    public async Task SaveAsync(ExternalServiceConfiguration config, CancellationToken ct = default)
    {
        var existing = await dbContext.ExternalServiceConfigurations.FindAsync([config.ServiceName], ct);
        if (existing is null)
        {
            dbContext.ExternalServiceConfigurations.Add(config);
        }
        else
        {
            existing.BaseUrl = config.BaseUrl;
            existing.ApiKey = config.ApiKey;
            existing.IsEnabled = config.IsEnabled;
        }
        await dbContext.SaveChangesAsync(ct);
    }
}