using FinanceManager.Domain.Entities;
using FinanceManager.Domain.Entities.Accounts;
using FinanceManager.Domain.Entities.Accounts.Entries;
using FinanceManager.Domain.Repositories;
using FinanceManager.Domain.Repositories.Account;

namespace FinanceManager.Application.Services;

public class DuplicateEntryResolverService(IBankAccountRepository<BankAccount> bankAccountRepository,
    IAccountEntryRepository<BankAccountEntry> accountEntryRepository, IDuplicateEntryRepository duplicateEntryRepository)
{
    public async Task<bool> Resolve(int accountId, int duplicateId, int entryIdToBeRemained)
    {
        var existingDuplicate = await duplicateEntryRepository.GetDuplicate(accountId, duplicateId);
        if (existingDuplicate is null) return false;

        foreach (var entryId in existingDuplicate.EntriesId)
        {
            if (entryId == entryIdToBeRemained) continue;
            await accountEntryRepository.Delete(accountId, entryId);
        }

        await duplicateEntryRepository.RemoveDuplicate(existingDuplicate.Id);

        return true;
    }
    public async Task Scan(int accountId)
    {
        if (!(await bankAccountRepository.Exists(accountId))) return;

        var oldestEntry = await accountEntryRepository.GetOldest(accountId);
        if (oldestEntry is null) return;

        var youngestEntry = await accountEntryRepository.GetYoungest(accountId);
        if (youngestEntry is null) return;

        for (var i = oldestEntry.PostingDate; i <= youngestEntry.PostingDate; i = i.AddDays(1))
        {
            var entries = await accountEntryRepository.Get(accountId, i, i.AddDays(1)).ToListAsync();
            var groups = entries.GroupBy(x => new { x.PostingDate, x.ValueChange }).Where(g => g.Count() > 1);

            var duplicates = groups.Select(x => new DuplicateEntry()
            {
                AccountId = accountId,
                EntriesId = x.Select(y => y.EntryId).ToList()
            }).ToList();

            if (duplicates is null || duplicates.Count == 0) continue;
            List<DuplicateEntry> duplicatesToAdd = [];

            foreach (var duplicate in duplicates)
            {
                DuplicateEntry? existingDuplicate = null;
                foreach (var entryId in duplicate.EntriesId)
                {
                    existingDuplicate = await duplicateEntryRepository.GetDuplicateByEntry(duplicate.AccountId, entryId);
                    if (existingDuplicate is not null) break;
                }

                if (existingDuplicate is not null)
                    await duplicateEntryRepository.UpdateDuplicate(existingDuplicate.Id, existingDuplicate.AccountId, duplicate.EntriesId);
                else
                    duplicatesToAdd.Add(duplicate);
            }

            await duplicateEntryRepository.AddDuplicate(duplicatesToAdd);
        }
    }
}