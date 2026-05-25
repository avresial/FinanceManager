using FinanceManager.Domain.Enums;

namespace FinanceManager.Application.Commands.Account;

public record UpdateAccount(int AccountId, string AccountName, AccountLabel AccountType);