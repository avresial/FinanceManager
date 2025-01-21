using FinanceManager.Domain.Entities.Accounts.Entries;
using FinanceManager.Domain.Repositories.Account;

namespace FinanceManager.Infrastructure.Repositories.Account.Entry;
public class InMemoryBankEntryRepository : IAccountEntryRepository<BankAccountEntry>
{
    private List<BankAccountEntry> _entries = [];
    public bool Add(BankAccountEntry entry)
    {
        var newEntry = new BankAccountEntry(entry.AccountId, entry.EntryId, entry.PostingDate, entry.Value, entry.ValueChange)
        {
            Description = entry.Description,
            ExpenseType = entry.ExpenseType,
        };

        _entries.Add(newEntry);
        return true;
    }

    public bool Delete(int accountId, int entryId)
    {
        throw new NotImplementedException();
    }

    public BankAccountEntry? Get(int accountId, DateTime startDate, DateTime endDate)
    {
        throw new NotImplementedException();
    }

    public BankAccountEntry? GetOldest(int accountId)
    {
        throw new NotImplementedException();
    }

    public BankAccountEntry? GetYoungest(int accountId)
    {
        throw new NotImplementedException();
    }

    public bool Update(BankAccountEntry entry)
    {
        throw new NotImplementedException();
    }
}
