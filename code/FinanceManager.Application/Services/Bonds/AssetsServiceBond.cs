using FinanceManager.Domain.Entities.Bonds;
using FinanceManager.Domain.Entities.Currencies;
using FinanceManager.Domain.Entities.MoneyFlowModels;
using FinanceManager.Domain.Enums;
using FinanceManager.Domain.Repositories.Account;
using FinanceManager.Domain.Services;
using System;

namespace FinanceManager.Application.Services.Bonds;

public class AssetsServiceBond(IFinancialAccountRepository financialAccountRepository) : IAssetsServiceTyped
{
    public bool IsOfType<T>() => typeof(T) == typeof(BondAccount);
    public async Task<List<TimeSeriesModel>> GetAssetsTimeSeries(int userId, Currency currency, DateTime start, DateTime end)
    {
        if (end > DateTime.UtcNow) end = DateTime.UtcNow;
        if (start == new DateTime()) return [];

        Dictionary<DateTime, decimal> prices = [];
        TimeSpan step = TimeSpan.FromDays(1);

        await foreach (BondAccount? account in financialAccountRepository.GetAccounts<BondAccount>(userId, start, end).Where(x => x.ContainsAssets))
        {
        }
        return [];
    }

    public async Task<List<TimeSeriesModel>> GetAssetsTimeSeries(int userId, Currency currency, DateTime start, DateTime end, InvestmentType investmentType)
    {
        if (end > DateTime.UtcNow) end = DateTime.UtcNow;
        List<(DateTime Date, decimal Value)> assets = [];

        await foreach (var account in financialAccountRepository.GetAccounts<BondAccount>(userId, start, end).Where(x => x.ContainsAssets && x.AccountType.ToString() == investmentType.ToString()))
            ;
        // assets.AddRange(account.Entries.GetAssets(start, end));
        return [];
    }

    public async IAsyncEnumerable<NameValueResult> GetEndAssetsPerAccount(int userId, Currency currency, DateTime asOfDate)
    {
        await foreach (var account in financialAccountRepository.GetAccounts<BondAccount>(userId, asOfDate.AddMinutes(-1), asOfDate).Where(x => x.ContainsAssets))
            yield return new(account.Name, account.Entries.First().Value);
    }

    public async IAsyncEnumerable<NameValueResult> GetEndAssetsPerType(int userId, Currency currency, DateTime asOfDate)
    {
        financialAccountRepository.GetAccounts<BondAccount>(userId, asOfDate.AddMinutes(-1), asOfDate).Select(x => x.AccountType).Distinct();

        Dictionary<AccountLabel, NameValueResult> accountLabelResults = [];

        await foreach (var account in financialAccountRepository.GetAccounts<BondAccount>(userId, asOfDate.AddMinutes(-1), asOfDate).Where(x => x.ContainsAssets))
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

        return financialAccountRepository.GetAccounts<BondAccount>(userId, end.AddDays(-1), end).AnyAsync(x => x.ContainsAssets).AsTask();
    }
}