using FinanceManager.Domain.Entities.FinancialAccounts.Currency;
using FinanceManager.Domain.Entities.MoneyFlowModels;
using FinanceManager.Domain.Repositories.Account;
using FinanceManager.Domain.Services;

namespace FinanceManager.Application.Services;

public class LiabilitiesService(IFinancialAccountRepository financialAccountService) : ILiabilitiesService
{
    public async Task<bool> IsAnyAccountWithLiabilities(int userId)
    {
        var bankAccounts = financialAccountService.GetAccounts<CurrencyAccount>(userId, DateTime.UtcNow.AddDays(-1), DateTime.UtcNow);
        await foreach (var bankAccount in bankAccounts)
        {
            if (bankAccount.Entries is not null && bankAccount.Entries.Count > 0)
            {
                var youngestEntry = bankAccount.Entries.FirstOrDefault();
                if (youngestEntry is not null && youngestEntry.Value < 0)
                    return true;
            }
            else if (bankAccount.NextOlderEntry is not null && bankAccount.NextOlderEntry.Value < 0)
            {
                return true;
            }
        }

        return await Task.FromResult(false);
    }
    public async IAsyncEnumerable<NameValueResult> GetEndLiabilitiesPerAccount(int userId, DateTime start, DateTime end)
    {
        await foreach (CurrencyAccount account in financialAccountService.GetAccounts<CurrencyAccount>(userId, start, end))
        {
            if (account is null || account.Entries is null) continue;
            var entry = account.Entries.FirstOrDefault();

            if (entry is null)
            {
                if (account.NextOlderEntry is null) continue;

                entry = account.NextOlderEntry;
            }

            if (entry.Value > 0) continue;

            yield return new NameValueResult()
            {
                Name = account.Name,
                Value = entry.Value
            };
        }
    }
    public async IAsyncEnumerable<NameValueResult> GetEndLiabilitiesPerType(int userId, DateTime start, DateTime end)
    {
        await foreach (var accounts in financialAccountService.GetAccounts<CurrencyAccount>(userId, start, end).GroupBy(x => x.AccountType))
        {
            NameValueResult? result = null;
            foreach (var account in accounts)
            {
                if (account is null || account.Entries is null) continue;
                var entry = account.Entries.FirstOrDefault();

                if (entry is null)
                {
                    if (account.NextOlderEntry is null) continue;

                    entry = account.NextOlderEntry;
                }

                if (entry.Value > 0) continue;


                if (result is null)
                {
                    result = new()
                    {
                        Name = account.AccountType.ToString(),
                        Value = entry.Value
                    };
                }
                else
                {
                    result.Value += entry.Value;
                }
            }
            if (result is not null)
                yield return result;
        }
    }
    public async IAsyncEnumerable<TimeSeriesModel> GetLiabilitiesTimeSeries(int userId, DateTime start, DateTime end)
    {
        if (start == new DateTime()) yield break;

        Dictionary<DateTime, decimal> prices = [];
        TimeSpan step = new(1, 0, 0, 0);

        await foreach (var account in financialAccountService.GetAccounts<CurrencyAccount>(userId, start, end))
        {
            if (account is null || account.Entries is null) continue;

            decimal previousValue = 0;

            if (account.Entries.Count != 0 && account.Entries.Last().PostingDate.Date == start.Date)
                previousValue = account.Entries.Last().Value - account.Entries.Last().ValueChange;
            else if (account.NextOlderEntry is not null)
                previousValue = account.NextOlderEntry.Value;

            if (previousValue > 0) continue;


            for (var date = start; date <= end; date = date.Add(step))
            {
                if (!prices.ContainsKey(date)) prices.Add(date, 0);

                var newestEntry = account.Get(date).OrderByDescending(x => x.PostingDate).FirstOrDefault();
                if (newestEntry is null)
                {
                    prices[date] += previousValue;
                    continue;
                }

                if (newestEntry.Value > 0) break;

                prices[date] += newestEntry.Value;
                previousValue = prices[date];
            }
        }

        var timeBucket = TimeBucketService.Get(prices.Select(x => (x.Key, x.Value))).OrderByDescending(x => x.Date);

        foreach (var price in timeBucket.Select(x => new TimeSeriesModel() { DateTime = x.Date, Value = x.Objects.Last() }))
            yield return price;
    }
}