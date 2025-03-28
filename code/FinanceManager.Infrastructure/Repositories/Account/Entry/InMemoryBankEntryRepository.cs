using FinanceManager.Domain.Entities.Accounts;
using FinanceManager.Domain.Entities.Accounts.Entries;
using FinanceManager.Domain.Repositories.Account;

namespace FinanceManager.Infrastructure.Repositories.Account.Entry;
public class InMemoryBankEntryRepository : IAccountEntryRepository<BankAccountEntry>
{
    private List<BankAccount> BankAccounts = [];
    public bool Add(BankAccountEntry entry)
    {
        BankAccount? bankAccount = BankAccounts.FirstOrDefault(x => x.AccountId == entry.AccountId);
        if (bankAccount is null)
        {
            bankAccount = new BankAccount(1, entry.AccountId, "", Domain.Enums.AccountType.Other);
            BankAccounts.Add(bankAccount);
        }
        bankAccount.AddEntry(new AddBankEntryDto(entry.PostingDate, entry.ValueChange, entry.ExpenseType, entry.Description));
        return true;
    }

    public bool Delete(int accountId, int entryId)
    {
        var bankAccount = BankAccounts.FirstOrDefault(x => x.AccountId == accountId);
        if (bankAccount is null) return false;
        bankAccount.Remove(entryId);
        return true;
    }

    public IEnumerable<BankAccountEntry> Get(int accountId, DateTime startDate, DateTime endDate)
    {
        var bankAccount = BankAccounts.FirstOrDefault(x => x.AccountId == accountId);
        if (bankAccount is null) return [];
        return bankAccount.Get(startDate, endDate);
    }

    public BankAccountEntry? GetOldest(int accountId)
    {
        var bankAccount = BankAccounts.FirstOrDefault(x => x.AccountId == accountId);
        if (bankAccount is null) return null;
        if (bankAccount.Entries is null || !bankAccount.Entries.Any()) return null;
        var maxDate = bankAccount.Entries.Min(x => x.PostingDate);

        return bankAccount.Entries.First(x => x.PostingDate == maxDate);
    }

    public BankAccountEntry? GetYoungest(int accountId)
    {
        var bankAccount = BankAccounts.FirstOrDefault(x => x.AccountId == accountId);
        if (bankAccount is null) return null;
        if (bankAccount.Entries is null || !bankAccount.Entries.Any()) return null;
        var minDate = bankAccount.Entries.Max(x => x.PostingDate);

        return bankAccount.Entries.First(x => x.PostingDate == minDate);
    }

    public bool Update(BankAccountEntry entry)
    {
        var bankAccount = BankAccounts.FirstOrDefault(x => x.AccountId == entry.AccountId);
        if (bankAccount is null) return false;
        if (bankAccount.Entries is null || !bankAccount.Entries.Any()) return false;

        var entryToUpdate = bankAccount.Entries.FirstOrDefault(x => x.EntryId == entry.EntryId);

        if (entryToUpdate is null) return false;

        entryToUpdate.Update(entry);
        return true;
    }
}
