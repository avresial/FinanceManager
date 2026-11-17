using FinanceManager.Contracts.FinancialAccounts.Investments;

namespace FinanceManager.Application.FinancialAccounts.Investments.Transactions;

public interface IInvestmentTransactionService
{
    Task<InvestmentTransactionDto> AddAsync(
        AddInvestmentTransactionRequest request,
        long userId,
        CancellationToken cancellationToken = default);
}