using FinanceManager.Domain.Entities;
using FinanceManager.Domain.Entities.Accounts;
using FinanceManager.Domain.Entities.Accounts.Entries;
using FinanceManager.Domain.Repositories;
using FinanceManager.Domain.Repositories.Account;

namespace FinanceManager.Application.Services;

public class DuplicateEntryResolverService(IBankAccountRepository<BankAccount> bankAccountRepository,
    IAccountEntryRepository<BankAccountEntry> accountEntryRepository, IDuplicateEntryRepository duplicateEntryRepository)
{
    private readonly IBankAccountRepository<BankAccount> _bankAccountRepository = bankAccountRepository;
    private readonly IAccountEntryRepository<BankAccountEntry> _accountEntryRepository = accountEntryRepository;
    private readonly IDuplicateEntryRepository _duplicateEntryRepository = duplicateEntryRepository;

    public async Task<bool> Resolve(int accountId, int duplicateId, int entryIdToBeRemained)
    {
        var existingDuplicate = await _duplicateEntryRepository.GetDuplicate(accountId, duplicateId);
        if (existingDuplicate is null) return false;

        foreach (var entryId in existingDuplicate.EntriesId)
        {
            if (entryId == entryIdToBeRemained) continue;
            await _accountEntryRepository.Delete(accountId, entryId);
        }

        await _duplicateEntryRepository.RemoveDuplicate(existingDuplicate.Id);

        return true;
    }
    public async Task Scan(int accountId)
    {
        if (!(await _bankAccountRepository.Exists(accountId))) return;

        var oldestEntry = await _accountEntryRepository.GetOldest(accountId);
        if (oldestEntry is null) return;

        for (var i = oldestEntry.PostingDate; i <= DateTime.UtcNow; i = i.AddDays(1))
        {
            var entries = await _accountEntryRepository.Get(accountId, i, i.AddDays(1));

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
                    existingDuplicate = await _duplicateEntryRepository.GetDuplicateByEntry(duplicate.AccountId, entryId);
                    if (existingDuplicate is not null) break;
                }

                if (existingDuplicate is not null)
                    await _duplicateEntryRepository.UpdateDuplicate(existingDuplicate.Id, existingDuplicate.AccountId, duplicate.EntriesId);
                else
                    duplicatesToAdd.Add(duplicate);
            }

            await _duplicateEntryRepository.AddDuplicate(duplicatesToAdd);
        }
    }


}
