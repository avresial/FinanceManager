using FinanceManager.Application.Identity.Users;
using FinanceManager.Domain.Administration.Monitoring;
using FinanceManager.Domain.Administration.Users;
using FinanceManager.Domain.FinancialAccounts.Bond.Entities;
using FinanceManager.Domain.FinancialAccounts.Bond.Repositories;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Currencies.Repositories;
using FinanceManager.Domain.FinancialAccounts.Currencies.Services;
using FinanceManager.Domain.FinancialAccounts.Investments.Entities;
using FinanceManager.Domain.FinancialAccounts.Investments.Services;
using FinanceManager.Domain.FinancialAccounts.Shared.Repositories;
using FinanceManager.Domain.FinancialAccounts.Shared.Services;
using FinanceManager.Domain.Identity.Entities;
using FinanceManager.Domain.Identity.Repositories;
using FinanceManager.Domain.Identity.Services;
using FinanceManager.Domain.Shared.Charting;

namespace FinanceManager.Application.Administration.Users;

public class AdministrationUsersService(IFinancialAccountRepository financialAccountRepository, IUserRepository userRepository,
    IActiveUsersRepository activeUsersRepository, IUserPlanVerifier userPlanVerifier,
    ICurrencyRepository currencyRepository, ICurrencyExchangeService currencyExchangeService,
    IBondDetailsRepository bondDetailsRepository, IInvestmentValuationService investmentValuationService) : IAdministrationUsersService
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
    public async Task<decimal?> GetTotalTrackedMoney()
    {
        var now = DateTime.UtcNow;
        var pln = await currencyRepository.GetByCode("PLN")
            ?? throw new InvalidOperationException("PLN currency is required to value tracked money.");
        var bondDetails = await bondDetailsRepository.GetAllAsync().ToDictionaryAsync(x => x.Id);
        decimal total = 0;

        // Queries share a scoped DbContext, so enumerate users and accounts sequentially.
        var userIds = await userRepository.GetUsersIds(0, int.MaxValue).ToListAsync();
        foreach (var userId in userIds)
        {
            var cashAccounts = await financialAccountRepository.GetAccounts<CurrencyAccount>(userId, now, now, false).ToListAsync();
            foreach (var account in cashAccounts)
            {
                var value = account.GetThisOrNextOlder(now)?.Value ?? 0;
                if (value == 0) continue;
                var currency = await currencyRepository.GetCurrency(account.CurrencyId)
                    ?? throw new InvalidOperationException($"Currency {account.CurrencyId} is required to value account {account.AccountId}.");
                total += await ConvertToPln(value, currency);
            }

            var bonds = await financialAccountRepository.GetAccounts<BondAccount>(userId, now, now, false).ToListAsync();
            foreach (var account in bonds)
            {
                foreach (var bondId in account.GetStoredBondsIds())
                {
                    var entry = account.GetThisOrNextOlder(now, bondId);
                    if (entry is null) continue;
                    if (!bondDetails.TryGetValue(bondId, out var details))
                        throw new InvalidOperationException($"Bond valuation requires details for bond id {bondId}.");
                    total += await ConvertToPln(entry.GetPriceAt(DateOnly.FromDateTime(now), details), details.Currency);
                }
            }

            var investments = await financialAccountRepository.GetAccounts<InvestmentAccount>(userId, now, now, false).ToListAsync();
            var investmentValues = await investmentValuationService.GetAccountValueAsync(investments.Select(x => x.AccountId).ToList(), pln, now);
            total += investmentValues.Values.Sum();
        }
        return total;

        async Task<decimal> ConvertToPln(decimal value, Currency currency)
        {
            if (value == 0 || string.Equals(currency.ShortName, "PLN", StringComparison.OrdinalIgnoreCase)) return value;
            var rate = await currencyExchangeService.GetExchangeRateAsync(currency, pln, now);
            if (rate is not decimal usableRate || usableRate <= 0)
                throw new InvalidOperationException($"No exchange rate from {currency.ShortName} to PLN is available.");
            return value * usableRate;
        }
    }
    public async IAsyncEnumerable<UserDetails> GetUsers(int recordIndex, int recordsCount)
    {
        var users = await userRepository.GetUsers(recordIndex, recordsCount).ToListAsync();
        if (users.Count == 0) yield break;

        var userIds = users.Select(x => x.UserId).ToList();
        var usedCapacities = await userPlanVerifier.GetUsedRecordsCapacity(userIds);
        var lastLoginTimes = await activeUsersRepository.GetLastLoginTimes(userIds);

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
                },
                LastLoggedAt = lastLoginTimes.TryGetValue(user.UserId, out var lastLogin) ? lastLogin : null
            };
        }
    }
    public Task<int> GetUsersCount() => userRepository.GetUsersCount();
}