using FinanceManager.Domain.Transactions.Investments.Repositories;

namespace FinanceManager.Application.Portfolio.Performance;

internal static class PortfolioHoldings
{
    /// <summary>The owner's non-zero signed units per listing, pooled across accounts and unlinked trades, as of the date.</summary>
    public static async Task<IReadOnlyDictionary<long, decimal>> LoadAsync(
        IInvestmentTransactionRepository investmentTransactionRepository,
        int userId,
        DateOnly asOf,
        CancellationToken cancellationToken)
    {
        var holdings = await investmentTransactionRepository.GetHoldingsByUserAsOf(userId, asOf, cancellationToken);
        return holdings.ToDictionary(holding => holding.AssetListingId, holding => holding.Units);
    }
}