using FinanceManager.Contracts.FinancialAccounts.Investments;

namespace FinanceManager.Contracts.Dashboard;

public record InvestmentTransactionLogItemDto(int? LegacyAccountId, InvestmentTransactionDto Transaction)
    : TransactionLogItemDto(LegacyAccountId);