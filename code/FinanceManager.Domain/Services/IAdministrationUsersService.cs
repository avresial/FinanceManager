using FinanceManager.Domain.Entities;
using FinanceManager.Domain.Entities.User;

namespace FinanceManager.Domain.Services;
public interface IAdministrationUsersService
{
    Task<int> GetAccountsCount();
    Task<IEnumerable<ChartEntryModel>> GetDailyActiveUsers();
    Task<IEnumerable<ChartEntryModel>> GetNewUsersDaily();
    Task<int?> GetTotalTrackedMoney();
    Task<IEnumerable<UserDetails>> GetUsers(int recordIndex, int recordsCount);
    Task<int> GetUsersCount();
}