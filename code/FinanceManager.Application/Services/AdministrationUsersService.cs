using FinanceManager.Application.Providers;
using FinanceManager.Domain.Entities;
using FinanceManager.Domain.Entities.User;
using FinanceManager.Domain.Repositories;
using FinanceManager.Domain.Repositories.Account;
using FinanceManager.Domain.Services;
using Microsoft.Extensions.Logging;

namespace FinanceManager.Application.Services;
public class AdministrationUsersService(IFinancalAccountRepository financalAccountRepository, IUserRepository userRepository, IActiveUsersRepository activeUsersRepository, UserPlanVerifier userPlanVerifier, PricingProvider pricingProvider, ILogger<AdministrationUsersService> logger) : IAdministrationUsersService
{
    private readonly IFinancalAccountRepository _financalAccountRepository = financalAccountRepository;
    private readonly IUserRepository _userRepository = userRepository;
    private readonly IActiveUsersRepository _activeUsersRepository = activeUsersRepository;
    private readonly UserPlanVerifier _userPlanVerifier = userPlanVerifier;
    private readonly PricingProvider _pricingProvider = pricingProvider;
    private readonly ILogger<AdministrationUsersService> _logger = logger;

    public async Task<int?> GetAccountsCount()
    {
        return await Task.FromResult(_financalAccountRepository.GetAccountsCount());
    }
    public async Task<IEnumerable<ChartEntryModel>> GetDailyActiveUsers()
    {
        DateTime end = DateTime.Now.Date;
        DateTime start = end.AddDays(-31);

        List<ChartEntryModel> result = [];

        try
        {
            var activeUsers = await _activeUsersRepository.GetActiveUsersCount(DateOnly.FromDateTime(start), DateOnly.FromDateTime(DateTime.Now));

            for (DateTime i = start; i <= end; i = i.AddDays(1))
            {
                var usersCreatedAtDate = activeUsers.Where(x => x.Item1 == DateOnly.FromDateTime(i));

                if (usersCreatedAtDate is null || usersCreatedAtDate.Any())
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
            _logger.LogError(ex, $"Error getting accounts count");
        }

        return [];

    }
    public async Task<IEnumerable<ChartEntryModel>> GetNewUsersDaily()
    {
        DateTime end = new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day, 23, 59, 59);
        DateTime start = end.AddDays(-31);
        var users = await userRepository.GetUsers(start, end);
        List<ChartEntryModel> result = [];

        try
        {
            for (DateTime i = start; i <= end; i = i.AddDays(1))
            {
                var usersCreatedAtDate = users.Where(x => x.CreationDate.Date == i.Date).Count();
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
            _logger.LogError(ex, $"Error getting accounts count");
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
        var users = await _userRepository.GetUsers(recordIndex, recordsCount);
        return users.Select(users => new UserDetails()
        {
            Id = users.UserId,
            Login = users.Login,
            PricingLevel = users.PricingLevel,
            RecordCapacity = new Domain.Entities.Login.RecordCapacity()
            {
                UsedCapacity = _userPlanVerifier.GetUsedRecordsCapacity(users.UserId).Result,
                TotalCapacity = _pricingProvider.GetMaxAllowedEntries(users.PricingLevel)
            }
        }).ToList();

    }
    public async Task<int?> GetUsersCount()
    {
        return await _userRepository.GetUsersCount();
    }
}
