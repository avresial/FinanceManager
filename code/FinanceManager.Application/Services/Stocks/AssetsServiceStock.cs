using FinanceManager.Domain.Entities.Currencies;
using FinanceManager.Domain.Entities.MoneyFlowModels;
using FinanceManager.Domain.Entities.Stocks;
using FinanceManager.Domain.Enums;
using FinanceManager.Domain.Extensions;
using FinanceManager.Domain.Repositories.Account;
using FinanceManager.Domain.Services;

namespace FinanceManager.Application.Services;

internal class AssetsServiceStock(
    IFinancialAccountRepository financialAccountRepository,
    IStockPriceProvider stockPriceProvider,
    IStockUnrealizedGainLossCalculator stockUnrealizedGainLossCalculator) : IAssetsServiceTyped
{
    public bool IsOfType<T>() => typeof(T) == typeof(StockAccount);
    public async Task<List<TimeSeriesModel>> GetAssetsTimeSeries(int userId, Currency currency, DateTime start, DateTime end)
    {
        Dictionary<DateTime, decimal> prices = [];
        await foreach (var account in financialAccountRepository.GetAccounts<StockAccount>(userId, start, end).Where(x => x.ContainsAssets))
        {
            for (DateTime date = end; date >= start; date = date.AddDays(-1))
            {
                if (!prices.ContainsKey(date)) prices.Add(date, 0);

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
            decimal value = 0;
            foreach (var ticker in account.GetStoredTickers())
            {
                var pricePerUnit = await stockPriceProvider.GetPricePerUnitAsync(ticker, currency, asOfDate);
                var latestEntry = account.Get(asOfDate).First(x => x.Ticker == ticker);

                value += latestEntry.Value * pricePerUnit;
            }

            yield return new(account.Name, value);
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

    public async Task<List<UnrealizedGainLossAccountResult>> GetUnrealizedGainLossPerAccount(int userId, Currency currency, DateTime asOfDate)
    {
        var instrumentResults = await GetUnrealizedGainLossPerInstrument(userId, currency, asOfDate);
        var byAccount = instrumentResults.GroupBy(x => new { x.AccountId, x.AccountName });

        List<UnrealizedGainLossAccountResult> results = [];
        foreach (var accountGroup in byAccount)
        {
            var included = accountGroup.Where(x => !x.IsExcludedFromTotals).ToList();
            var excludedCount = accountGroup.Count(x => x.IsExcludedFromTotals);

            var costBasis = included.Sum(x => x.CostBasis);
            var currentValue = included.Sum(x => x.CurrentValue);
            var unrealized = currentValue - costBasis;
            var unrealizedPercent = costBasis == 0 ? 0 : unrealized / costBasis * 100;

            results.Add(new UnrealizedGainLossAccountResult(
                accountGroup.Key.AccountId,
                accountGroup.Key.AccountName,
                costBasis,
                currentValue,
                unrealized,
                unrealizedPercent,
                asOfDate,
                excludedCount
            ));
        }

        return results;
    }

    public async Task<List<UnrealizedGainLossInstrumentResult>> GetUnrealizedGainLossPerInstrument(int userId, Currency currency, DateTime asOfDate)
    {
        List<UnrealizedGainLossInstrumentResult> results = [];

        await foreach (var account in financialAccountRepository.GetAccounts<StockAccount>(userId, DateTime.MinValue, asOfDate).Where(x => x.ContainsAssets))
        {
            foreach (var ticker in account.GetStoredTickers())
            {
                var instrumentResult = await stockUnrealizedGainLossCalculator.CalculateAsync(account, ticker, currency, asOfDate);
                if (instrumentResult is not null)
                    results.Add(instrumentResult);
            }
        }

        return results;
    }

}