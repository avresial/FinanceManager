
namespace FinanceManager.Application.Identity.Users;

public interface IUserPlanVerifier
{
    Task<bool> CanAddMoreAccounts(int userId);
    Task<bool> CanAddMoreEntries(int userId, int entriesCount = 1);
    Task<int> GetUsedRecordsCapacity(int userId);

    /// <summary>
    /// Returns the used record capacity for each supplied user, computed with a single grouped count
    /// query. Every requested user id is present in the result; users with no entries map to zero.
    /// </summary>
    Task<IReadOnlyDictionary<int, int>> GetUsedRecordsCapacity(IReadOnlyCollection<int> userIds);
}