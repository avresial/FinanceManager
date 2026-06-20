using FinanceManager.Domain.FinancialAccounts.Investments.Entities;

namespace FinanceManager.Domain.FinancialAccounts.Investments.Repositories;

public interface IInvestmentTransactionRepository
{
    Task<InvestmentTransaction?> Get(long id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<InvestmentTransaction>> GetByAccount(int accountId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<InvestmentTransaction>> GetByUser(long userId, DateOnly startDate, DateOnly endDate, CancellationToken cancellationToken = default);
    Task<InvestmentTransaction> Add(InvestmentTransaction transaction, CancellationToken cancellationToken = default);
    Task<bool> Update(InvestmentTransaction transaction, CancellationToken cancellationToken = default);
    Task<bool> Delete(long id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Signed-quantity holdings per <see cref="AssetListing"/> as of a date, summed across the given accounts.
    /// Replaces the old per-ISIN running-balance boundary lookups with a single grouped query.
    /// </summary>
    Task<IReadOnlyDictionary<long, decimal>> GetHoldingsAsOf(IReadOnlyCollection<int> accountIds, DateOnly asOf, CancellationToken cancellationToken = default);
}