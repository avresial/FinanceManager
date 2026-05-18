using FinanceManager.Domain.Entities.Ai;
using FinanceManager.Domain.Repositories;
using FinanceManager.Infrastructure.Contexts;
using Microsoft.EntityFrameworkCore;

namespace FinanceManager.Infrastructure.Repositories;

internal sealed class AiProviderConfigRepository(AppDbContext dbContext) : IAiProviderConfigRepository
{
    public Task<List<AiProviderConfiguration>> GetProvidersAsync(CancellationToken ct = default) =>
        dbContext.AiProviderConfigurations.AsNoTracking().ToListAsync(ct);

    public Task<List<AiFallbackEntry>> GetFallbackEntriesAsync(CancellationToken ct = default) =>
        dbContext.AiFallbackEntries.AsNoTracking().OrderBy(e => e.Order).ToListAsync(ct);

    public async Task SaveProviderAsync(AiProviderConfiguration config, CancellationToken ct = default)
    {
        var existing = await dbContext.AiProviderConfigurations.FindAsync([config.ProviderName], ct);
        if (existing is null)
        {
            dbContext.AiProviderConfigurations.Add(config);
        }
        else
        {
            existing.BaseUrl = config.BaseUrl;
            existing.ApiKey = config.ApiKey;
            existing.RequestTimeoutSeconds = config.RequestTimeoutSeconds;
            existing.IsEnabled = config.IsEnabled;
        }
        await dbContext.SaveChangesAsync(ct);
    }

    public async Task SaveFallbackEntriesAsync(List<AiFallbackEntry> entries, CancellationToken ct = default)
    {
        var existing = await dbContext.AiFallbackEntries.ToListAsync(ct);
        dbContext.AiFallbackEntries.RemoveRange(existing);

        for (var i = 0; i < entries.Count; i++)
        {
            entries[i].Order = i;
            entries[i].Id = 0;
        }
        dbContext.AiFallbackEntries.AddRange(entries);
        await dbContext.SaveChangesAsync(ct);
    }
}
