using FinanceManager.Domain.Entities;

namespace FinanceManager.Domain.Repositories;
public interface IDuplicateEntryRepository
{
    Task AddDuplicate(DuplicateEntry duplicate);
    Task AddDuplicate(IEnumerable<DuplicateEntry> duplicate);

    Task RemoveDuplicate(int duplicateId);
    Task RemoveDuplicate(IEnumerable<DuplicateEntry> duplicate);

    Task<int> GetDuplicatesCount(int accountId);
    Task<DuplicateEntry?> GetDuplicate(int accountId, int duplicateId);
    Task<DuplicateEntry?> GetDuplicateByEntry(int accountId, int entryIndex);
    Task<IEnumerable<DuplicateEntry>> GetDuplicates(int accountId, int index, int count);
    Task<DuplicateEntry> UpdateDuplicate(int duplicateId, int accountId, IEnumerable<int> newEntryIds);
}
