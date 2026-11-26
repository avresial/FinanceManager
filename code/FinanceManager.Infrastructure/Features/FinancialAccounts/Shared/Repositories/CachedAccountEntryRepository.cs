using FinanceManager.Domain.Dashboard.Services;
using FinanceManager.Domain.FinancialAccounts.Shared.Entities;
using FinanceManager.Domain.FinancialAccounts.Shared.Repositories;
using FinanceManager.Domain.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using System.Runtime.CompilerServices;

namespace FinanceManager.Infrastructure.Features.FinancialAccounts.Shared.Repositories;

/// <summary>Invalidates the owner's derived reads after entry mutations.</summary>
public class CachedAccountEntryRepository<T>(
    IAccountEntryRepository<T> inner,
    ICacheInvalidator cacheInvalidator) : IAccountEntryRepository<T> where T : class, ILedgerEntry
{
    public Task<T?> GetByOwner(int userId, long entryId, CancellationToken cancellationToken = default) => inner.GetByOwner(userId, entryId, cancellationToken);
    public IAsyncEnumerable<T> GetByOwner(int userId, int partitionId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default) =>
        inner.GetByOwner(userId, partitionId, startDate, endDate, cancellationToken);
    public Task<IReadOnlyList<T>> GetByIds(int userId, IReadOnlyCollection<long> entryIds, CancellationToken cancellationToken = default) => inner.GetByIds(userId, entryIds, cancellationToken);

    public Task<IReadOnlyList<T>> GetRecentUnlabelled(int count, CancellationToken cancellationToken = default) => inner.GetRecentUnlabelled(count, cancellationToken);

    public Task<IReadOnlyDictionary<int, int>> GetEntriesCountPerUser(IReadOnlyCollection<int> userIds, CancellationToken cancellationToken = default) => inner.GetEntriesCountPerUser(userIds, cancellationToken);

    // ----- Mutating methods: invalidate the owning user's cache at the write boundary -----

    public async Task<bool> Add(T entry, CancellationToken cancellationToken = default)
    {
        try
        {
            return await inner.Add(entry, cancellationToken);
        }
        finally
        {
            await InvalidateEntryOwners([entry]);
        }
    }

    public async Task<bool> Add(
        IEnumerable<T> entries,
        CancellationToken cancellationToken = default)
    {
        var entryList = entries as IList<T> ?? entries.ToList();
        try
        {
            return await inner.Add(entryList, cancellationToken);
        }
        finally
        {
            await InvalidateEntryOwners(entryList);
        }
    }

    public async Task<bool> UpdateByOwner(int userId, T entry, CancellationToken cancellationToken = default)
    {
        try
        {
            return await inner.UpdateByOwner(userId, entry, cancellationToken);
        }
        finally
        {
            await InvalidateOwner(userId);
        }
    }

    public async Task<bool> DeleteByOwner(int userId, long entryId, CancellationToken cancellationToken = default)
    {
        try
        {
            return await inner.DeleteByOwner(userId, entryId, cancellationToken);
        }
        finally
        {
            await InvalidateOwner(userId);
        }
    }

    public async Task<int> AddLabels(int userId, IEnumerable<(long entryId, int labelId)> labelAssignments, CancellationToken cancellationToken = default)
    {
        var assignments = labelAssignments as IList<(long entryId, int labelId)> ?? labelAssignments.ToList();
        var result = await inner.AddLabels(userId, assignments, cancellationToken);
        // The owner's cache tags cover linked and unlinked entries alike, and the invalidation is decoupled from the caller's token:
        // once the write has committed the cache must be invalidated even if the request is being cancelled.
        if (result > 0) await InvalidateOwner(userId);
        return result;
    }

    // Guest owners have negative ids, so only zero is rejected. A committed write must bust the cache even when
    // the request is being cancelled, hence no caller token.
    private async Task InvalidateOwner(int userId)
    {
        if (userId != 0)
            await cacheInvalidator.InvalidateUser(userId, CancellationToken.None);
    }

    private async Task InvalidateEntryOwners(IEnumerable<T> entries)
    {
        foreach (var owner in entries.Select(entry => entry.UserId).Distinct())
            await InvalidateOwner(owner);
    }
}