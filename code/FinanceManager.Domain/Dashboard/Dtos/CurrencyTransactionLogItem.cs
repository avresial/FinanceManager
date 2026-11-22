using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;

namespace FinanceManager.Domain.Dashboard.Dtos;

public record CurrencyTransactionLogItem(int AccountId, string AccountName, int CurrencyId, string Currency, CurrencyAccountEntry Entry)
    : TransactionLogItem(AccountId, AccountName);