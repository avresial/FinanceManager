using FinanceManager.Domain.Entities;
using FinanceManager.Domain.Entities.Accounts;
using FinanceManager.Domain.Entities.MoneyFlowModels;
using FinanceManager.Domain.Enums;
using FinanceManager.Domain.Extensions;
using FinanceManager.Domain.Repositories.Account;
using FinanceManager.Domain.Services;

namespace FinanceManager.Application.Services;
internal class AssetsServiceStock(IFinancialAccountRepository financialAccountRepository, IStockPriceProvider stockPriceProvider) : IAssetsServiceTyped
{
    public bool IsOfType<T>() => typeof(T) == typeof(StockAccount);
    public async Task<List<TimeSeriesModel>> GetAssetsTimeSeries(int userId, Currency currency, DateTime start, DateTime end)
    {
        Dictionary<DateTime, decimal> prices = [];
        TimeSpan step = new(1, 0, 0, 0);
        await foreach (var account in financialAccountRepository.GetAccounts<StockAccount>(userId, start, end).Where(x => x.ContainsAssets))
        {
            foreach (var date in prices.Keys)
            {
                foreach (var ticker in account.GetStoredTickers())
                {
                    var entry = account.GetThisOrNextOlder(date, ticker);
                    if (entry is null) continue;

                    var pricePerUnit = await stockPriceProvider.GetPricePerUnitAsync(ticker, currency, end);
                    prices[date] += entry.Value * pricePerUnit;
                }
            }
        }

        return prices.Select(x => new TimeSeriesModel(x.Key, x.Value)).ToList();
    }

    public async Task<List<TimeSeriesModel>> GetAssetsTimeSeries(int userId, Currency currency, DateTime start, DateTime end, InvestmentType investmentType)
    {
        List<(DateTime Date, decimal Value)> assets = [];

        await foreach (var account in financialAccountRepository.GetAccounts<StockAccount>(userId, start, end).Where(x => x.ContainsAssets))
            assets.AddRange(await account.Entries.Where(x => x.InvestmentType == investmentType)
                .GetAssets(start, end, currency, stockPriceProvider.GetPricePerUnitAsync));

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
        await foreach (var account in financialAccountRepository.GetAccounts<StockAccount>(userId, asOfDate.AddMinutes(-1), asOfDate).Where(x => x.ContainsAssets))
        {
            NameValueResult result = new(account.Name, 0);

            foreach (var ticker in account.GetStoredTickers())
            {
                var pricePerUnit = await stockPriceProvider.GetPricePerUnitAsync(ticker, currency, asOfDate);
                var latestEntry = account.Get(asOfDate).First(x => x.Ticker == ticker);

                result.Value += latestEntry.Value * pricePerUnit;
            }

            yield return result;
        }
    }

    public async IAsyncEnumerable<NameValueResult> GetEndAssetsPerType(int userId, Currency currency, DateTime asOfDate)
    {
        Dictionary<InvestmentType, NameValueResult> investmentTypeResults = [];
        await foreach (var account in financialAccountRepository.GetAccounts<StockAccount>(userId, asOfDate.AddMinutes(-1), asOfDate).Where(x => x.ContainsAssets))
        {
            if (!account.ContainsAssets) continue;

            foreach (var ticker in account.GetStoredTickers())
            {
                var pricePerUnit = await stockPriceProvider.GetPricePerUnitAsync(ticker, currency, asOfDate);
                var latestEntry = account.Entries.First(x => x.Ticker == ticker);

                if (!investmentTypeResults.TryGetValue(latestEntry.InvestmentType, out NameValueResult? existingResult))
                    investmentTypeResults.Add(latestEntry.InvestmentType, new(latestEntry.InvestmentType.ToString(), latestEntry.Value * pricePerUnit));
                else
                    existingResult.Value += latestEntry.Value * pricePerUnit;
            }
        }

        foreach (var value in investmentTypeResults.Values)
            yield return value;
    }

    public Task<bool> IsAnyAccountWithAssets(int userId)
    {
        var end = DateTime.UtcNow;

        return financialAccountRepository.GetAccounts<StockAccount>(userId, end.AddDays(-1), end).AnyAsync(x => x.ContainsAssets).AsTask();
    }

}
