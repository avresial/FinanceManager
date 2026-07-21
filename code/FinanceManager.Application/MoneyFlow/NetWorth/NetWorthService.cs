using FinanceManager.Domain.FinancialAccounts.Bond.Entities;
using FinanceManager.Domain.FinancialAccounts.Bond.Repositories;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Investments.Entities;
using FinanceManager.Domain.FinancialAccounts.Investments.Services;
using FinanceManager.Domain.FinancialAccounts.Shared.Entities;
using FinanceManager.Domain.FinancialAccounts.Shared.Repositories;
using FinanceManager.Domain.FinancialAccounts.Shared.Services;
using FinanceManager.Domain.Identity.Repositories;
using FinanceManager.Domain.Identity.Services;
using FinanceManager.Domain.MoneyFlow.Services;

namespace FinanceManager.Application.MoneyFlow.NetWorth;

public class NetWorthService(IFinancialAccountRepository financialAccountRepository,
IBondDetailsRepository bondDetailsRepository, IInvestmentValuationService investmentValuationService) : INetWorthService
{
    public async Task<decimal?> GetNetWorth(int userId, Currency currency, DateTime date)
    {
        if (date > DateTime.UtcNow) date = DateTime.UtcNow;
        decimal result = 0;
        var bondDetails = await bondDetailsRepository.GetAllAsync().ToDictionaryAsync(x => x.Id);

        await foreach (var account in financialAccountRepository.GetAccounts<CurrencyAccount>(userId, date.Date, date))
        {
            var newestEntry = account.GetThisOrNextOlder(date);
            if (newestEntry is null) continue;

            result += newestEntry.Value;
        }

        await foreach (var account in financialAccountRepository.GetAccounts<BondAccount>(userId, date.Date, date))
        {
            foreach (var detailsId in account.GetStoredBondsIds())
            {
                var newestEntry = account.GetThisOrNextOlder(date, detailsId);
                if (newestEntry is null) continue;
                if (!bondDetails.TryGetValue(detailsId, out var details))
                    throw new InvalidOperationException($"Bond valuation requires details for bond id {detailsId}.");

                result += newestEntry.GetPriceAt(DateOnly.FromDateTime(date), details);
            }
        }

        await foreach (var account in financialAccountRepository.GetAccounts<InvestmentAccount>(userId, date.Date, date))
            result += await investmentValuationService.GetAccountValueAsync(account.AccountId, currency, date);

        return Math.Round(result, 2);
    }
    public async Task<Dictionary<DateTime, decimal>> GetNetWorth(int userId, Currency currency, DateTime start, DateTime end)
    {
        if (start == new DateTime()) return [];
        if (end > DateTime.UtcNow) end = DateTime.UtcNow;

        Dictionary<DateTime, decimal> result = [];

        var bondDetails = await bondDetailsRepository.GetAllAsync().ToDictionaryAsync(x => x.Id);

        List<CurrencyAccount> currencyAccounts = [];
        await foreach (var account in financialAccountRepository.GetAccounts<CurrencyAccount>(userId, start, end))
            currencyAccounts.Add(account);

        List<BondAccount> bondAccounts = [];
        await foreach (var account in financialAccountRepository.GetAccounts<BondAccount>(userId, start, end))
            bondAccounts.Add(account);

        List<InvestmentAccount> investmentAccounts = [];
        await foreach (var account in financialAccountRepository.GetAccounts<InvestmentAccount>(userId, start, end))
            investmentAccounts.Add(account);

        // Investment accounts (new asset model) value per account/day through the valuation service.
        // One batched call issues a single transactions query for all accounts and prices each
        // distinct listing once across them, rather than a per-account round-trip on the shared
        // AppDbContext (which EF Core cannot service concurrently anyway).
        var investmentValuesByAccount = await investmentValuationService.GetAccountValueSeriesAsync(
            investmentAccounts.Select(a => a.AccountId).ToList(), currency, start.Date, end.Date);

        for (DateTime date = end; date >= start; date = date.AddDays(-1))
        {
            decimal dailyTotal = 0;

            foreach (var account in currencyAccounts)
            {
                var entry = account.GetThisOrNextOlder(date);
                if (entry is null) continue;
                dailyTotal += entry.Value;
            }

            foreach (var account in bondAccounts)
            {
                foreach (var detailsId in account.GetStoredBondsIds())
                {
                    var entry = account.GetThisOrNextOlder(date, detailsId);
                    if (entry is null) continue;
                    if (!bondDetails.TryGetValue(detailsId, out var details))
                        throw new InvalidOperationException($"Bond valuation requires details for bond id {detailsId}.");

                    dailyTotal += entry.GetPriceAt(DateOnly.FromDateTime(date), details);
                }
            }

            foreach (var account in investmentAccounts)
            {
                if (investmentValuesByAccount.TryGetValue(account.AccountId, out var investmentSeries)
                    && investmentSeries.TryGetValue(date.Date, out var investmentValue))
                {
                    dailyTotal += investmentValue;
                }
            }

            result.Add(date, Math.Round(dailyTotal, 2));
        }
        return result;
    }
}