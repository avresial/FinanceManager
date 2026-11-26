using FinanceManager.Contracts.FinancialAccounts.Investments;
using FinanceManager.Contracts.Transactions.Investments;

namespace FinanceManager.Application.FinancialAccounts.Investments.Transactions;

public interface IInvestmentTransactionService
{
    /// <summary>Adds an unlinked trade to the owner's pooled portfolio with the same validation and import pipeline.</summary>
    Task<InvestmentTransactionDto> AddOwnerAsync(
        CreateInvestmentTradeRequest request,
        long userId,
        CancellationToken cancellationToken = default);
}