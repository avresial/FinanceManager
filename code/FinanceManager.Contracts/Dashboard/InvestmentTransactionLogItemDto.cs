using FinanceManager.Contracts.FinancialAccounts.Investments;

namespace FinanceManager.Contracts.Dashboard;

public record InvestmentTransactionLogItemDto(InvestmentTransactionDto Transaction)
    : TransactionLogItemDto;