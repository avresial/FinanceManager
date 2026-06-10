
namespace FinanceManager.Application.Identity.Users;

public interface IUserPlanVerifier
{
    Task<bool> CanAddMoreAccounts(int userId);
    Task<bool> CanAddMoreEntries(int userId, int entriesCount = 1);
    Task<int> GetUsedRecordsCapacity(int userId);
}