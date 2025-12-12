using FinanceManager.Application.Providers;
using FinanceManager.Domain.Entities.Shared;
using FinanceManager.Domain.Entities.Users;
using FinanceManager.Domain.Repositories;
using FinanceManager.Domain.Repositories.Account;
using FinanceManager.Domain.Services;

namespace FinanceManager.Application.Services;

public class AdministrationUsersService(IFinancialAccountRepository financialAccountRepository, IUserRepository userRepository,
    IActiveUsersRepository activeUsersRepository, IUserPlanVerifier userPlanVerifier) : IAdministrationUsersService
{

    public Task<int> GetAccountsCount() => financialAccountRepository.GetAccountsCount();

    public async IAsyncEnumerable<ChartEntryModel> GetDailyActiveUsers()
    {
        var end = DateTime.UtcNow.AddDays(1).Date.AddTicks(-1);
        var start = end.AddDays(-31);
        var activeUsers = await activeUsersRepository.GetActiveUsersCount(DateOnly.FromDateTime(start), DateOnly.FromDateTime(end));

        for (DateTime i = start; i <= end; i = i.AddDays(1))
        {
            var usersCreatedAtDate = activeUsers.Where(x => x.Item1 == DateOnly.FromDateTime(i));
            ChartEntryModel value = new(i, 0);

            if (usersCreatedAtDate is not null && usersCreatedAtDate.Any())
                value.Value = usersCreatedAtDate.First().Item2;

            yield return value;
        }
    }
    public async IAsyncEnumerable<ChartEntryModel> GetNewUsersDaily()
    {
        var end = DateTime.UtcNow.AddDays(1).Date.AddTicks(-1);
        var start = end.AddDays(-31);
        var users = await userRepository.GetUsers(start, end).ToListAsync();

        for (DateTime i = start; i <= end; i = i.AddDays(1))
            yield return new(i, users.Count(x => x.CreationDate.Date == i.Date));
    }
    public Task<int?> GetTotalTrackedMoney() => Task.FromResult<int?>(null);
    public async IAsyncEnumerable<UserDetails> GetUsers(int recordIndex, int recordsCount)
    {

        foreach (var userId in await userRepository.GetUsersIds(recordIndex, recordsCount).ToListAsync())
        {
            var user = await userRepository.GetUser(userId);
            if (user is null) continue;

            yield return new()
            {
                UserId = user.UserId,
                Login = user.Login,
                PricingLevel = user.PricingLevel,
                RecordCapacity = new RecordCapacity()
                {
                    UsedCapacity = userPlanVerifier.GetUsedRecordsCapacity(user.UserId).Result,
                    TotalCapacity = PricingProvider.GetMaxAllowedEntries(user.PricingLevel)
                }
            };
        }
    }
    public Task<int> GetUsersCount() => userRepository.GetUsersCount();
}