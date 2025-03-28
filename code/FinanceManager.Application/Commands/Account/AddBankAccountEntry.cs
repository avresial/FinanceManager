using FinanceManager.Domain.Entities.Accounts.Entries;

namespace FinanceManager.Application.Commands.Account;

public record AddBankAccountEntry(BankAccountEntry entry);
