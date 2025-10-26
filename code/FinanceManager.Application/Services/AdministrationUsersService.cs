using FinanceManager.Application.Providers;
using FinanceManager.Domain.Entities;
using FinanceManager.Domain.Entities.User;
using FinanceManager.Domain.Repositories;
using FinanceManager.Domain.Repositories.Account;
using FinanceManager.Domain.Services;
using Microsoft.Extensions.Logging;

namespace FinanceManager.Application.Services;
public class AdministrationUsersService(IFinancialAccountRepository financialAccountRepository, IUserRepository userRepository,
    IActiveUsersRepository activeUsersRepository, IUserPlanVerifier userPlanVerifier, PricingProvider pricingProvider,
    ILogger<AdministrationUsersService> logger) : IAdministrationUsersService
{

    public Task<int> GetAccountsCount() => financialAccountRepository.GetAccountsCount();

    public async Task<IEnumerable<ChartEntryModel>> GetDailyActiveUsers()
    {
        DateTime end = DateTime.Now.Date;
        DateTime start = end.AddDays(-31);

        List<ChartEntryModel> result = [];

        try
        {
            var activeUsers = await activeUsersRepository.GetActiveUsersCount(DateOnly.FromDateTime(start), DateOnly.FromDateTime(DateTime.Now));

            for (DateTime i = start; i <= end; i = i.AddDays(1))
            {
                var usersCreatedAtDate = activeUsers.Where(x => x.Item1 == DateOnly.FromDateTime(i));
                if (usersCreatedAtDate is null || !usersCreatedAtDate.Any())
                {
                    result.Add(new ChartEntryModel()
                    {
                        Date = i,
                        Value = 0
                    });
                }
                else
                {
                    result.Add(new ChartEntryModel()
                    {
                        Date = i,
                        Value = usersCreatedAtDate.First().Item2
                    });
                }
            }

            return result;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"Error getting daily active users");
        }

        return [];

    }
    public async Task<IEnumerable<ChartEntryModel>> GetNewUsersDaily()
    {
        DateTime end = new(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day, 23, 59, 59);
        DateTime start = end.AddDays(-31);
        var users = await userRepository.GetUsers(start, end);
        List<ChartEntryModel> result = [];

        try
        {
            for (DateTime i = start; i <= end; i = i.AddDays(1))
            {
                var usersCreatedAtDate = users.Count(x => x.CreationDate.Date == i.Date);
                result.Add(new ChartEntryModel()
                {
                    Date = i,
                    Value = usersCreatedAtDate
                });
            }
            return result;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"Error getting new users daily");
        }

        return [];
    }
    public async Task<int?> GetTotalTrackedMoney()
    {
        int? result = null;
        return await Task.FromResult(result);
    }
    public async Task<IEnumerable<UserDetails>> GetUsers(int recordIndex, int recordsCount)
    {
        var users = await userRepository.GetUsers(recordIndex, recordsCount);

        return users.Select(users => new UserDetails()
        {
            UserId = users.UserId,
            Login = users.Login,
            PricingLevel = users.PricingLevel,
            RecordCapacity = new Domain.Entities.Login.RecordCapacity()
            {
                UsedCapacity = userPlanVerifier.GetUsedRecordsCapacity(users.UserId).Result,
                TotalCapacity = pricingProvider.GetMaxAllowedEntries(users.PricingLevel)
            }
        }).ToList();

    }
    public Task<int> GetUsersCount() => userRepository.GetUsersCount();
}
