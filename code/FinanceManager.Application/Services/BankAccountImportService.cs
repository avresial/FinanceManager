using FinanceManager.Domain.Entities.Accounts;
using FinanceManager.Domain.Entities.Accounts.Entries;
using FinanceManager.Domain.Entities.Imports;
using FinanceManager.Domain.Repositories.Account;

namespace FinanceManager.Application.Services;

public class BankAccountImportService
{
    private readonly IBankAccountRepository<BankAccount> _bankAccountRepository;
    private readonly IAccountEntryRepository<BankAccountEntry> _bankAccountEntryRepository;
    private readonly UserPlanVerifier _userPlanVerifier;

    public BankAccountImportService(IBankAccountRepository<BankAccount> bankAccountRepository,
        IAccountEntryRepository<BankAccountEntry> bankAccountEntryRepository,
        UserPlanVerifier userPlanVerifier)
    {
        _bankAccountRepository = bankAccountRepository;
        _bankAccountEntryRepository = bankAccountEntryRepository;
        _userPlanVerifier = userPlanVerifier;
    }

    public async Task<ImportResult> ImportEntries(int userId, int accountId, IEnumerable<BankEntryImport> entries)
    {
        if (entries is null) throw new ArgumentNullException(nameof(entries));

        var entryList = entries.ToList();

        if (!await _userPlanVerifier.CanAddMoreEntries(userId, entryList.Count))
            throw new InvalidOperationException("Plan does not allow importing this many entries.");

        var account = await _bankAccountRepository.Get(accountId);
        if (account is null || account.UserId != userId) throw new InvalidOperationException("Account not found or access denied.");

        int imported = 0;
        int failed = 0;
        var errors = new List<string>();

        foreach (var entry in entryList)
        {
            var newEntry = new BankAccountEntry(accountId, 0, entry.PostingDate, entry.ValueChange, entry.ValueChange)
            {
                Description = string.Empty,
                Labels = new List<FinancialLabel>()
            };

            try
            {
                var ok = await _bankAccountEntryRepository.Add(newEntry);
                if (ok) imported++; else { failed++; errors.Add($"Failed to import entry with date {entry.PostingDate}."); }
            }
            catch (Exception ex)
            {
                failed++;
                errors.Add(ex.Message);
            }
        }

        return new ImportResult(accountId, imported, failed, errors);
    }
}
