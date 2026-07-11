using FinanceManager.Domain.Identity.Entities;
using FinanceManager.Domain.Shared.Charting;

namespace FinanceManager.Domain.Administration.Users;

public interface IAdministrationUsersService
{
    Task<int> GetAccountsCount();
    IAsyncEnumerable<ChartEntryModel> GetDailyActiveUsers();
    IAsyncEnumerable<ChartEntryModel> GetNewUsersDaily();
    Task<int?> GetTotalTrackedMoney();
    IAsyncEnumerable<UserDetails> GetUsers(int recordIndex, int recordsCount);
    Task<int> GetUsersCount();
}