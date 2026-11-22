using FinanceManager.Contracts.FinancialAccounts.Bond;

namespace FinanceManager.Contracts.Dashboard;

public record BondTransactionLogItemDto(int AccountId, string AccountName, BondAccountEntryDto Entry)
    : TransactionLogItemDto(AccountId, AccountName);