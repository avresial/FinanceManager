using FinanceManager.Domain.Entities.Shared.Accounts;
using FinanceManager.Domain.FinancialAccounts.Bond.Entities;
using FinanceManager.Domain.FinancialAccounts.Bond.Repositories;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Stock.Entities;
using FinanceManager.Domain.FinancialAccounts.Stock.Services;
using FinanceManager.Domain.Identity.Repositories;
using FinanceManager.Domain.Identity.Services;
using FinanceManager.Domain.MoneyFlow.Services;
using FinanceManager.Domain.Repositories;
using FinanceManager.Domain.Repositories.Account;
using FinanceManager.Domain.Services;

namespace FinanceManager.Application.MoneyFlow.NetWorth;

public class NetWorthService(IFinancialAccountRepository financialAccountRepository, IStockPriceProvider stockPriceProvider,
IBondDetailsRepository bondDetailsRepository) : INetWorthService
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

        await foreach (var account in financialAccountRepository.GetAccounts<StockAccount>(userId, date.Date, date))
        {
            foreach (var ticker in account.GetStoredTickers())
            {
                var newestEntry = account.GetThisOrNextOlder(date, ticker);
                if (newestEntry is null) continue;

                decimal pricePerUnit = await stockPriceProvider.GetPricePerUnitAsync(ticker, currency, date);
                result += newestEntry.Value * pricePerUnit;
            }
        }

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

        List<StockAccount> stockAccounts = [];
        await foreach (var account in financialAccountRepository.GetAccounts<StockAccount>(userId, start, end))
            stockAccounts.Add(account);

        Dictionary<int, List<string>> tickersByAccount = stockAccounts.ToDictionary(
            x => x.AccountId,
            x => x.GetStoredTickers().Concat(x.NextOlderEntries.Keys).Distinct(StringComparer.OrdinalIgnoreCase).ToList());

        Dictionary<string, IReadOnlyDictionary<DateTime, decimal>> pricesByTicker = new(StringComparer.OrdinalIgnoreCase);
        var tickers = tickersByAccount.Values.SelectMany(x => x).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (tickers.Count > 0)
        {
            var preloadTasks = tickers.ToDictionary(
                ticker => ticker,
                ticker => stockPriceProvider.GetPricePerUnitSeriesAsync(ticker, currency, start.Date, end.Date),
                StringComparer.OrdinalIgnoreCase);

            await Task.WhenAll(preloadTasks.Values);
            foreach (var preloadTask in preloadTasks)
                pricesByTicker[preloadTask.Key] = await preloadTask.Value;
        }

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

            foreach (var account in stockAccounts)
            {
                foreach (var ticker in tickersByAccount[account.AccountId])
                {
                    var entry = account.GetThisOrNextOlder(date, ticker);
                    if (entry is null) continue;
                    if (!pricesByTicker.TryGetValue(ticker, out var tickerPrices)) continue;
                    if (!tickerPrices.TryGetValue(date.Date, out var pricePerUnit)) continue;

                    dailyTotal += entry.Value * pricePerUnit;
                }
            }

            result.Add(date, Math.Round(dailyTotal, 2));
        }
        return result;
    }
}