using FinanceManager.Domain.Entities;
using FinanceManager.Domain.Entities.Accounts;
using FinanceManager.Domain.Entities.MoneyFlowModels;
using FinanceManager.Domain.Enums;
using FinanceManager.Domain.Extensions;
using FinanceManager.Domain.Repositories.Account;
using FinanceManager.Domain.Services;

namespace FinanceManager.Application.Services;
internal class AssetsServiceBank(IFinancialAccountRepository financialAccountRepository) : IAssetsServiceTyped
{
    public bool IsOfType<T>() => typeof(T) == typeof(BankAccount);

    public async Task<List<TimeSeriesModel>> GetAssetsTimeSeries(int userId, Currency currency, DateTime start, DateTime end)
    {
        if (end > DateTime.UtcNow) end = DateTime.UtcNow;
        if (start == new DateTime()) return [];

        Dictionary<DateTime, decimal> prices = [];
        TimeSpan step = new(1, 0, 0, 0);

        await foreach (var account in financialAccountRepository.GetAccounts<BankAccount>(userId, start, end).Where(x => x.ContainsAssets))
        {
            var entry = account.GetThisOrNextOlder(end);
            if (entry is null || entry.Value <= 0) continue;

            decimal previousValue = entry.Value;

            for (DateTime date = start; date <= end; date = date.Add(step))
            {
                if (!prices.ContainsKey(date)) prices.Add(date, 0);

                var newestEntry = account.GetThisOrNextOlder(date);
                if (newestEntry is null)
                {
                    prices[date] += previousValue;
                    continue;
                }

                prices[date] += newestEntry.Value;
                previousValue = prices[date];
            }
        }

        return prices.Select(x => new TimeSeriesModel(x.Key, x.Value)).ToList();
    }

    public async Task<List<TimeSeriesModel>> GetAssetsTimeSeries(int userId, Currency currency, DateTime start, DateTime end, InvestmentType investmentType)
    {
        if (end > DateTime.UtcNow) end = DateTime.UtcNow;
        List<(DateTime Date, decimal Value)> assets = [];

        await foreach (var account in financialAccountRepository.GetAccounts<BankAccount>(userId, start, end).Where(x => x.ContainsAssets && x.AccountType.ToString() == investmentType.ToString()))
            assets.AddRange(account.Entries.GetAssets(start, end));

        List<TimeSeriesModel> result = [];
        for (DateTime i = end; i >= start; i = i.AddDays(-1))
        {
            var assetsToSum = assets.Where(x => x.Date == i);
            result.Add(new(i, assetsToSum.Any() ? assetsToSum.Sum(x => x.Value) : 0));
        }

        return result;
    }

    public async IAsyncEnumerable<NameValueResult> GetEndAssetsPerAccount(int userId, Currency currency, DateTime asOfDate)
    {
        await foreach (var account in financialAccountRepository.GetAccounts<BankAccount>(userId, asOfDate.AddMinutes(-1), asOfDate).Where(x => x.ContainsAssets))
            yield return new(account.Name, account.Entries.First().Value);
    }

    public async IAsyncEnumerable<NameValueResult> GetEndAssetsPerType(int userId, Currency currency, DateTime asOfDate)
    {
        Dictionary<AccountLabel, NameValueResult> accountLabelResults = [];

        await foreach (var account in financialAccountRepository.GetAccounts<BankAccount>(userId, asOfDate.AddMinutes(-1), asOfDate).Where(x => x.ContainsAssets))
        {
            var entry = account.Entries.Count != 0 ? account.Entries.FirstOrDefault() : account.NextOlderEntry;
            if (entry is null || entry.Value <= 0) continue;

            var existingResult = accountLabelResults.ContainsKey(account.AccountType) ? accountLabelResults[account.AccountType] : null;
            if (existingResult is null)
                accountLabelResults.Add(account.AccountType, new(account.AccountType.ToString(), entry.Value));
            else
                existingResult.Value += entry.Value;
        }

        foreach (var value in accountLabelResults.Values)
            yield return value;
    }

    public Task<bool> IsAnyAccountWithAssets(int userId)
    {
        var end = DateTime.UtcNow;

        return financialAccountRepository.GetAccounts<BankAccount>(userId, end.AddDays(-1), end).AnyAsync(x => x.ContainsAssets).AsTask();
    }
}