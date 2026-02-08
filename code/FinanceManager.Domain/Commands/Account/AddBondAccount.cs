namespace FinanceManager.Domain.Commands.Account;

public record AddBondAccount(string AccountName) : AddAccount(AccountName);