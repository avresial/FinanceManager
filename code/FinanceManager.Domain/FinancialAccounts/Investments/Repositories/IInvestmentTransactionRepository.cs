using FinanceManager.Domain.Assets.Entities;
using FinanceManager.Domain.FinancialAccounts.Investments.Entities;

namespace FinanceManager.Domain.FinancialAccounts.Investments.Repositories;

public interface IInvestmentTransactionRepository
{
    /// <summary>Get a transaction by its database id, or <c>null</c> if none exists.</summary>
    Task<InvestmentTransaction?> Get(long id, CancellationToken cancellationToken = default);

    /// <summary>Get all transactions belonging to the given account.</summary>
    Task<IReadOnlyList<InvestmentTransaction>> GetByAccount(int accountId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all transactions belonging to any of the given accounts in a single query. Lets callers
    /// value several accounts without a per-account round-trip on the shared scoped context.
    /// </summary>
    Task<IReadOnlyList<InvestmentTransaction>> GetByAccounts(IReadOnlyCollection<int> accountIds, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get the <paramref name="count"/> most recent transactions across the given accounts in a single
    /// bounded query, ordered by <see cref="InvestmentTransaction.TradeDate"/> descending then id descending.
    /// Enforces the limit at the database level so callers composing recent-activity views (e.g. the
    /// dashboard transaction log) never materialise a whole transaction history.
    /// </summary>
    Task<IReadOnlyList<InvestmentTransaction>> GetMostRecentByAccounts(IReadOnlyCollection<int> accountIds, int count, CancellationToken cancellationToken = default);

    /// <summary>Get a user's transactions whose <see cref="InvestmentTransaction.TradeDate"/> falls within [startDate, endDate].</summary>
    Task<IReadOnlyList<InvestmentTransaction>> GetByUser(long userId, DateOnly startDate, DateOnly endDate, CancellationToken cancellationToken = default);

    /// <summary>Insert a new transaction and return the persisted entity (with its generated id).</summary>
    Task<InvestmentTransaction> Add(InvestmentTransaction transaction, CancellationToken cancellationToken = default);

    /// <summary>Update an existing transaction. Returns <c>false</c> when no transaction with that id exists.</summary>
    Task<bool> Update(InvestmentTransaction transaction, CancellationToken cancellationToken = default);

    /// <summary>Delete the transaction with the given id. Returns <c>false</c> when no such transaction exists.</summary>
    Task<bool> Delete(long id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Signed-quantity holdings per <see cref="AssetListing"/> as of a date, summed across the given accounts.
    /// Replaces the old per-ISIN running-balance boundary lookups with a single grouped query.
    /// </summary>
    Task<IReadOnlyDictionary<long, decimal>> GetHoldingsAsOf(IReadOnlyCollection<int> accountIds, DateOnly asOf, CancellationToken cancellationToken = default);
}