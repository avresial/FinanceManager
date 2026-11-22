using FinanceManager.Contracts.FinancialAccounts.Investments;

namespace FinanceManager.Contracts.Dashboard;

public record InvestmentTransactionLogItemDto(int AccountId, string AccountName, InvestmentTransactionDto Transaction)
    : TransactionLogItemDto(AccountId, AccountName);