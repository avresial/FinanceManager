using FinanceManager.Application.Identity.Users;
using FinanceManager.Domain.Administration.Monitoring;
using FinanceManager.Domain.Administration.Users;
using FinanceManager.Domain.Entities.Shared;
using FinanceManager.Domain.FinancialAccounts.Shared.Repositories;
using FinanceManager.Domain.FinancialAccounts.Shared.Services;
using FinanceManager.Domain.Identity.Entities;
using FinanceManager.Domain.Identity.Repositories;
using FinanceManager.Domain.Identity.Services;
using FinanceManager.Domain.Repositories;

namespace FinanceManager.Application.Administration.Users;

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
        var users = await userRepository.GetUsers(recordIndex, recordsCount).ToListAsync();
        if (users.Count == 0) yield break;

        var usedCapacities = await userPlanVerifier.GetUsedRecordsCapacity(users.Select(x => x.UserId).ToList());

        foreach (var user in users)
        {
            yield return new()
            {
                UserId = user.UserId,
                Login = user.Login,
                PricingLevel = user.PricingLevel,
                RecordCapacity = new RecordCapacity()
                {
                    UsedCapacity = usedCapacities.TryGetValue(user.UserId, out var used) ? used : 0,
                    TotalCapacity = PricingProvider.GetMaxAllowedEntries(user.PricingLevel)
                }
            };
        }
    }
    public Task<int> GetUsersCount() => userRepository.GetUsersCount();
}