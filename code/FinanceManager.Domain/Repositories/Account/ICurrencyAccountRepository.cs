using FinanceManager.Domain.Enums;

namespace FinanceManager.Domain.Repositories.Account;

public interface ICurrencyAccountRepository<T> : IAccountRepository<T>
{
    Task<int?> Add(int userId, string accountName, AccountLabel accountType);
    Task<bool> Update(int accountId, string accountName, AccountLabel accountType);

    /// <summary>
    /// Counts currency-account entries for each of the supplied users in a single grouped query.
    /// Users with no currency entries are omitted from the result, so callers should treat a missing
    /// key as a count of zero. Replaces the previous one-COUNT-per-account fan-out.
    /// </summary>
    Task<IReadOnlyDictionary<int, int>> GetEntriesCountPerUser(IReadOnlyCollection<int> userIds, CancellationToken cancellationToken = default);
}