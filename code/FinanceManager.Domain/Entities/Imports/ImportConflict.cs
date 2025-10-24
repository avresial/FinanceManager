using FinanceManager.Domain.Entities.Accounts.Entries;

namespace FinanceManager.Domain.Entities.Imports;

public record ImportConflict(int AccountId, BankEntryImport? Incoming, BankAccountEntry? ExistingMatch, string Reason);
