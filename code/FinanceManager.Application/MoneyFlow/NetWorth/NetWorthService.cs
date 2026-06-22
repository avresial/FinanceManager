using FinanceManager.Domain.FinancialAccounts.Bond.Entities;
using FinanceManager.Domain.FinancialAccounts.Bond.Repositories;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Investments.Services;
using FinanceManager.Domain.FinancialAccounts.Shared.Entities;
using FinanceManager.Domain.FinancialAccounts.Shared.Repositories;
using FinanceManager.Domain.FinancialAccounts.Shared.Services;
using FinanceManager.Domain.FinancialAccounts.Stock.Entities;
using FinanceManager.Domain.FinancialAccounts.Stock.Services;
using FinanceManager.Domain.Identity.Repositories;
using FinanceManager.Domain.Identity.Services;
using FinanceManager.Domain.MoneyFlow.Services;

namespace FinanceManager.Application.MoneyFlow.NetWorth;

public class NetWorthService(IFinancialAccountRepository financialAccountRepository, IStockPriceProvider stockPriceProvider,
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

        await foreach (var account in financialAccountRepository.GetAccounts<StockAccount>(userId, date.Date, date))
        {
            // Legacy stock accounts hold StockEntries and are valued per ticker below; investment
            // accounts (new asset model) hold no StockEntries, so the ticker loop is a no-op and the
            // value comes from the investment valuation service. The two are mutually exclusive per
            // account, so there is no double counting.
            foreach (var ticker in account.GetStoredTickers())
            {
                var newestEntry = account.GetThisOrNextOlder(date, ticker);
                if (newestEntry is null) continue;

                decimal pricePerUnit = await stockPriceProvider.GetPricePerUnitAsync(ticker, currency, date);
                result += newestEntry.Value * pricePerUnit;
            }

            result += await investmentValuationService.GetAccountValueAsync(account.AccountId, currency, date);
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

        // Investment accounts (new asset model) value per account/day through the valuation service.
        // Legacy stock accounts return an empty series here (no investment transactions), so each
        // Stock-type account contributes through exactly one path. Fan the per-account series fetches
        // out concurrently so latency does not grow linearly with the account count.
        var investmentSeriesTasks = stockAccounts.ToDictionary(
            account => account.AccountId,
            account => investmentValuationService.GetAccountValueSeriesAsync(account.AccountId, currency, start.Date, end.Date));
        await Task.WhenAll(investmentSeriesTasks.Values);

        Dictionary<int, IReadOnlyDictionary<DateTime, decimal>> investmentValuesByAccount = [];
        foreach (var (accountId, task) in investmentSeriesTasks)
            investmentValuesByAccount[accountId] = await task;

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