using FinanceManager.Application.Identity.Users;
using FinanceManager.Domain.Administration.Monitoring;
using FinanceManager.Domain.Administration.Users;
using FinanceManager.Domain.Currencies.Entities;
using FinanceManager.Domain.Currencies.Repositories;
using FinanceManager.Domain.Currencies.Services;
using FinanceManager.Domain.Identity.Entities;
using FinanceManager.Domain.Identity.Repositories;
using FinanceManager.Domain.Identity.Services;
using FinanceManager.Domain.Portfolio.Valuation.Services;
using FinanceManager.Domain.Shared.Charting;
using FinanceManager.Domain.Transactions.Bonds.Entities;
using FinanceManager.Domain.Transactions.Bonds.Repositories;
using FinanceManager.Domain.Transactions.Cash.Repositories;

namespace FinanceManager.Application.Administration.Users;

public class AdministrationUsersService(ICashTransactionReader cashTransactionReader, IBondTransactionReader bondTransactionReader, IUserRepository userRepository,
    IActiveUsersRepository activeUsersRepository, IUserPlanVerifier userPlanVerifier,
    ICurrencyRepository currencyRepository, ICurrencyExchangeService currencyExchangeService,
    IBondDetailsRepository bondDetailsRepository, IInvestmentValuationService investmentValuationService) : IAdministrationUsersService
{
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
        var currencies = new Dictionary<int, Currency>();
        decimal total = 0;

        // Queries share a scoped DbContext, so enumerate users sequentially. Each user's pooled positions
        // (entries linked to an account or not) are valued as of the same instant.
        var userIds = await userRepository.GetUsersIds(0, int.MaxValue).ToListAsync();
        foreach (var userId in userIds)
        {
            foreach (var position in (await cashTransactionReader.GetPositions(userId, now)).Where(p => p.Value != 0))
            {
                if (!currencies.TryGetValue(position.PartitionId, out var currency))
                {
                    currency = await currencyRepository.GetCurrency(position.PartitionId)
                        ?? throw new InvalidOperationException($"Currency {position.PartitionId} is required to value tracked cash.");
                    currencies.Add(position.PartitionId, currency);
                }
                total += await ConvertToPln(position.Value, currency);
            }

            foreach (var position in (await bondTransactionReader.GetPositions(userId, now)).Where(p => p.Value != 0))
            {
                if (!bondDetails.TryGetValue(position.PartitionId, out var details))
                    throw new InvalidOperationException($"Bond valuation requires details for bond id {position.PartitionId}.");
                var price = BondEntry.GetPrice(DateOnly.FromDateTime(position.LastPostingDate), position.Value, DateOnly.FromDateTime(now), details)
                    .GetValueOrDefault(DateOnly.FromDateTime(now));
                total += await ConvertToPln(price, details.Currency);
            }

            total += (await investmentValuationService.GetUserValueByListingAsync(userId, pln, now)).Values.Sum();
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