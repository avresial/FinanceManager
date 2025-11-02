using FinanceManager.Domain.Entities;
using FinanceManager.Domain.Entities.User;

namespace FinanceManager.Domain.Services;
public interface IAdministrationUsersService
{
    Task<int> GetAccountsCount();
    IAsyncEnumerable<ChartEntryModel> GetDailyActiveUsers();
    IAsyncEnumerable<ChartEntryModel> GetNewUsersDaily();
    Task<int?> GetTotalTrackedMoney();
    IAsyncEnumerable<UserDetails> GetUsers(int recordIndex, int recordsCount);
    Task<int> GetUsersCount();
}