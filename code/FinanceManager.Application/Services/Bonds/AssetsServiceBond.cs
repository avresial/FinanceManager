using FinanceManager.Domain.Entities.Bonds;
using FinanceManager.Domain.Entities.Currencies;
using FinanceManager.Domain.Entities.MoneyFlowModels;
using FinanceManager.Domain.Enums;
using FinanceManager.Domain.Repositories;
using FinanceManager.Domain.Repositories.Account;
using FinanceManager.Domain.Services;
using System;

namespace FinanceManager.Application.Services.Bonds;

public class AssetsServiceBond(IFinancialAccountRepository financialAccountRepository, IBondDetailsRepository bondDetailsRepository) : IAssetsServiceTyped
{
    public bool IsOfType<T>() => typeof(T) == typeof(BondAccount);
    public async Task<List<TimeSeriesModel>> GetAssetsTimeSeries(int userId, Currency currency, DateTime start, DateTime end)
    {
        if (end > DateTime.UtcNow) end = DateTime.UtcNow;
        if (start == new DateTime()) return [];

        Dictionary<DateTime, decimal> prices = [];
        TimeSpan step = TimeSpan.FromDays(1);
        List<BondDetails> bondDetails = await bondDetailsRepository.GetAllAsync().ToListAsync();
        await foreach (var account in financialAccountRepository.GetAccounts<BondAccount>(userId, start, end).Where(x => x.ContainsAssets))
        {
            foreach (var price in account.GetDailyPrice(bondDetails))
            {
                if (!prices.ContainsKey(price.Key.ToDateTime(TimeOnly.MinValue)))
                    prices.Add(price.Key.ToDateTime(TimeOnly.MinValue), price.Value);
                else
                    prices[price.Key.ToDateTime(TimeOnly.MinValue)] += price.Value;
            }
        }

        return prices.Select(x => new TimeSeriesModel(x.Key, x.Value)).ToList();
    }

    public async Task<List<TimeSeriesModel>> GetAssetsTimeSeries(int userId, Currency currency, DateTime start, DateTime end, InvestmentType investmentType)
    {
        if (end > DateTime.UtcNow) end = DateTime.UtcNow;
        if (start == new DateTime()) return [];

        Dictionary<DateTime, decimal> prices = [];
        TimeSpan step = TimeSpan.FromDays(1);
        List<BondDetails> bondDetails = await bondDetailsRepository.GetAllAsync().ToListAsync();
        await foreach (var account in financialAccountRepository.GetAccounts<BondAccount>(userId, start, end).Where(x => x.ContainsAssets && x.AccountType.ToString() == investmentType.ToString()))
        {
            foreach (var price in account.GetDailyPrice(bondDetails))
            {
                if (!prices.ContainsKey(price.Key.ToDateTime(TimeOnly.MinValue)))
                    prices.Add(price.Key.ToDateTime(TimeOnly.MinValue), price.Value);
                else
                    prices[price.Key.ToDateTime(TimeOnly.MinValue)] += price.Value;
            }
        }

        return prices.Select(x => new TimeSeriesModel(x.Key, x.Value)).ToList();
    }

    public async IAsyncEnumerable<NameValueResult> GetEndAssetsPerAccount(int userId, Currency currency, DateTime asOfDate)
    {
        await foreach (var account in financialAccountRepository.GetAccounts<BondAccount>(userId, asOfDate.AddMinutes(-1), asOfDate).Where(x => x.ContainsAssets))
            yield return new(account.Name, account.Entries.First().Value);
    }

    public async IAsyncEnumerable<NameValueResult> GetEndAssetsPerType(int userId, Currency currency, DateTime asOfDate)
    {
        await foreach (var account in financialAccountRepository.GetAccounts<BondAccount>(userId, asOfDate.AddMinutes(-1), asOfDate).Where(x => x.ContainsAssets))
        {
            decimal value = 0;
            foreach (var bondDetailsId in account.GetStoredBondsIds())
            {
                var latestEntry = account.Get(asOfDate).First(x => x.BondDetailsId == bondDetailsId);

                value += latestEntry.Value;
            }

            yield return new(account.Name, value);
        }
    }

    public Task<bool> IsAnyAccountWithAssets(int userId)
    {
        var end = DateTime.UtcNow;

        return financialAccountRepository.GetAccounts<BondAccount>(userId, end.AddDays(-1), end).AnyAsync(x => x.ContainsAssets).AsTask();
    }
}