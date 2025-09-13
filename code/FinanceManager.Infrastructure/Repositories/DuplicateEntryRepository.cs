using FinanceManager.Domain.Entities;
using FinanceManager.Domain.Repositories;
using FinanceManager.Infrastructure.Contexts;
using Microsoft.EntityFrameworkCore;

namespace FinanceManager.Infrastructure.Repositories;
internal class DuplicateEntryRepository(AppDbContext context) : IDuplicateEntryRepository
{

    public async Task AddDuplicate(DuplicateEntry duplicate)
    {
        await context.DuplicateEntries.AddAsync(duplicate);
        await context.SaveChangesAsync();
    }

    public async Task AddDuplicate(IEnumerable<DuplicateEntry> duplicates)
    {
        foreach (var duplicate in duplicates)
            if (duplicate.EntriesId == null || !duplicate.EntriesId.Any())
                throw new ArgumentException("DuplicateEntry must have at least one entry ID.", nameof(duplicates));


        await context.DuplicateEntries.AddRangeAsync(duplicates);
        await context.SaveChangesAsync();
    }

    public Task<DuplicateEntry?> GetDuplicate(int accountId, int duplicateId) =>
        context.DuplicateEntries.FirstOrDefaultAsync(x => x.AccountId == accountId && x.Id == duplicateId);

    public Task<DuplicateEntry?> GetDuplicateByEntry(int accountId, int entryIndex) =>
     context.DuplicateEntries.FirstOrDefaultAsync(x => x.AccountId == accountId && x.EntriesId.Contains(entryIndex));

    public async Task<IEnumerable<DuplicateEntry>> GetDuplicates(int accountId, int index, int count)
    {
        return await context.DuplicateEntries
            .Where(x => x.AccountId == accountId)
            .OrderBy(x => x.Id)
            .Skip(index)
            .Take(count)
            .ToListAsync();
    }

    public Task<int> GetDuplicatesCount(int accountId) =>
        context.DuplicateEntries.CountAsync(x => x.AccountId == accountId);


    public async Task RemoveDuplicate(int duplicateId)
    {
        var entity = await context.DuplicateEntries.FindAsync(duplicateId);
        if (entity != null)
        {
            context.DuplicateEntries.Remove(entity);
            await context.SaveChangesAsync();
        }
    }

    public async Task RemoveDuplicate(IEnumerable<DuplicateEntry> duplicates)
    {
        context.DuplicateEntries.RemoveRange(duplicates);
        await context.SaveChangesAsync();
    }

    public async Task<DuplicateEntry> UpdateDuplicate(int duplicateId, int accountId, List<int> newEntryIds)
    {
        var entity = await context.DuplicateEntries.FirstOrDefaultAsync(x => x.Id == duplicateId && x.AccountId == accountId);

        if (entity == null) throw new InvalidOperationException("DuplicateEntry not found");

        entity.EntriesId = newEntryIds;
        await context.SaveChangesAsync();
        return entity;
    }
}
