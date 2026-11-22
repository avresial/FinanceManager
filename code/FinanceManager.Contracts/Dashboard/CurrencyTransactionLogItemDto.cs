using FinanceManager.Contracts.FinancialAccounts.Currencies;

namespace FinanceManager.Contracts.Dashboard;

public record CurrencyTransactionLogItemDto(int AccountId, string AccountName, int CurrencyId, string Currency, CurrencyAccountEntryDto Entry)
    : TransactionLogItemDto(AccountId, AccountName);