using FinanceManager.Domain.Administration.Logging;
using FinanceManager.Domain.Identity.Entities;
using FinanceManager.Domain.Identity.Repositories;
using FinanceManager.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FinanceManager.Infrastructure.Features.Administration.Repositories;

public class LogEntryRepository(AppDbContext context) : ILogEntryRepository
{
    public async Task AddRange(IEnumerable<LogEntry> entries, CancellationToken cancellationToken = default)
    {
        context.LogEntries.AddRange(entries);
        await context.SaveChangesAsync(cancellationToken);
    }

    public Task<List<LogEntry>> GetLatest(int count, IReadOnlyCollection<LogSeverity>? levels = null, CancellationToken cancellationToken = default)
    {
        var query = context.LogEntries.AsNoTracking();
        if (levels is { Count: > 0 })
            query = query.Where(e => levels.Contains(e.Level));

        return query
            .OrderByDescending(e => e.TimestampUtc)
            .ThenByDescending(e => e.Id)
            .Take(count)
            .ToListAsync(cancellationToken);
    }

    public async Task<(List<LogEntry> Items, int TotalCount)> GetPaged(
        int skip,
        int take,
        IReadOnlyCollection<LogSeverity>? levels = null,
        CancellationToken cancellationToken = default)
    {
        var query = context.LogEntries.AsNoTracking();
        if (levels is { Count: > 0 })
            query = query.Where(e => levels.Contains(e.Level));

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(e => e.TimestampUtc)
            .ThenByDescending(e => e.Id)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);

        return (items, total);
    }

    public Task<int> DeleteOlderThan(DateTime cutoffUtc, CancellationToken cancellationToken = default) =>
        context.LogEntries
            .Where(e => e.TimestampUtc < cutoffUtc)
            .ExecuteDeleteAsync(cancellationToken);
}