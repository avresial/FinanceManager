using FinanceManager.Domain.Identity.Entities;

namespace FinanceManager.Domain.Administration.Monitoring;

public interface IActiveUsersRepository
{
    Task Add(int userId, DateOnly dateOnly);
    Task<ActiveUser?> Get(int userId, DateOnly dateOnly);
    Task<int> GetActiveUsersCount(DateOnly dateOnly);
    Task<IEnumerable<(DateOnly, int)>> GetActiveUsersCount(DateOnly dateStart, DateOnly dateEnd);

    /// <summary>
    /// Returns the most recent login time for each of the given users, keyed by user id. Users that have never
    /// logged in are omitted from the result so callers can distinguish "never logged in" from a real timestamp.
    /// </summary>
    Task<IReadOnlyDictionary<int, DateTime>> GetLastLoginTimes(IReadOnlyCollection<int> userIds);
}