using FinanceManager.Contracts.Transactions.Investments;

namespace FinanceManager.Contracts.Dashboard;

public record InvestmentTransactionLogItemDto(InvestmentTransactionDto Transaction)
    : TransactionLogItemDto;