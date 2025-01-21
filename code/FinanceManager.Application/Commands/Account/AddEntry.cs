using FinanceManager.Domain.Entities.Accounts.Entries;

namespace FinanceManager.Application.Commands.Account;

public record AddEntry(BankAccountEntry entry);
