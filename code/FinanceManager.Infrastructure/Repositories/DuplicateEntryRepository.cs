using FinanceManager.Domain.Entities;
using FinanceManager.Domain.Repositories;
using FinanceManager.Infrastructure.Contexts;
using Microsoft.EntityFrameworkCore;

namespace FinanceManager.Infrastructure.Repositories;
internal class DuplicateEntryRepository(AppDbContext context) : IDuplicateEntryRepository
{
    private readonly AppDbContext _context = context;

    public async Task AddDuplicate(DuplicateEntry duplicate)
    {
        await _context.DuplicateEntries.AddAsync(duplicate);
        await _context.SaveChangesAsync();
    }

    public async Task AddDuplicate(IEnumerable<DuplicateEntry> duplicates)
    {
        await _context.DuplicateEntries.AddRangeAsync(duplicates);
        await _context.SaveChangesAsync();
    }

    public async Task<DuplicateEntry?> GetDuplicate(int accountId, int duplicateId)
    {
        return await _context.DuplicateEntries
            .FirstOrDefaultAsync(x => x.AccountId == accountId && x.Id == duplicateId);
    }

    public async Task<DuplicateEntry?> GetDuplicateByEntry(int accountId, int entryIndex)
    {
        return await _context.DuplicateEntries
            .FirstOrDefaultAsync(x => x.AccountId == accountId && x.EntriesId.Contains(entryIndex));
    }

    public async Task<IEnumerable<DuplicateEntry>> GetDuplicates(int accountId, int index, int count)
    {
        return await _context.DuplicateEntries
            .Where(x => x.AccountId == accountId)
            .OrderBy(x => x.Id)
            .Skip(index)
            .Take(count)
            .ToListAsync();
    }

    public async Task<int> GetDuplicatesCount(int accountId)
    {
        return await _context.DuplicateEntries.CountAsync(x => x.AccountId == accountId);
    }

    public async Task RemoveDuplicate(int duplicateId)
    {
        var entity = await _context.DuplicateEntries.FindAsync(duplicateId);
        if (entity != null)
        {
            _context.DuplicateEntries.Remove(entity);
            await _context.SaveChangesAsync();
        }
    }

    public async Task RemoveDuplicate(IEnumerable<DuplicateEntry> duplicates)
    {
        _context.DuplicateEntries.RemoveRange(duplicates);
        await _context.SaveChangesAsync();
    }

    public async Task<DuplicateEntry> UpdateDuplicate(int duplicateId, int accountId, IEnumerable<int> newEntryIds)
    {
        var entity = await _context.DuplicateEntries.FirstOrDefaultAsync(x => x.Id == duplicateId && x.AccountId == accountId);

        if (entity == null) throw new InvalidOperationException("DuplicateEntry not found");

        entity.EntriesId = newEntryIds;
        await _context.SaveChangesAsync();
        return entity;
    }
}
