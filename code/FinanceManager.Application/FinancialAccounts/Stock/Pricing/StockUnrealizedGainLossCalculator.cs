using FinanceManager.Domain.Entities.Currencies;
using FinanceManager.Domain.Entities.MoneyFlowModels;
using FinanceManager.Domain.Entities.Stocks;
using FinanceManager.Domain.Identity.Services;
using FinanceManager.Domain.Services;

namespace FinanceManager.Application.FinancialAccounts.Stock.Pricing;

internal class StockUnrealizedGainLossCalculator(IStockPriceProvider stockPriceProvider) : IStockUnrealizedGainLossCalculator
{
    public async Task<UnrealizedGainLossInstrumentResult?> CalculateAsync(StockAccount account, string ticker, Currency currency, DateTime asOfDate)
    {
        var latestEntry = account.GetThisOrNextOlder(asOfDate, ticker);
        if (latestEntry is null || latestEntry.Value <= 0) return null;

        decimal lastPurchuaseQuantity = latestEntry.Value;
        var buyEntries = account.Entries
            .Where(x => x.Isin == ticker && x.PostingDate <= asOfDate && x.ValueChange > 0)
            .ToList();

        decimal totalBoughtUnits = 0;
        decimal totalBoughtCost = 0;
        foreach (var buyEntry in buyEntries)
        {
            var buyPrice = await stockPriceProvider.GetPricePerUnitAsync(ticker, currency, buyEntry.PostingDate);
            if (buyPrice <= 0)
            {
                return new UnrealizedGainLossInstrumentResult(
                    account.AccountId,
                    account.Name,
                    ticker,
                    ticker,
                    lastPurchuaseQuantity,
                    0,
                    0,
                    0,
                    0,
                    asOfDate,
                    true,
                    "Missing historical buy price. Excluded from account unrealized totals."
                );
            }

            totalBoughtUnits += buyEntry.ValueChange;
            totalBoughtCost += buyEntry.ValueChange * buyPrice;
        }

        if (totalBoughtUnits <= 0)
        {
            return new UnrealizedGainLossInstrumentResult(
                account.AccountId,
                account.Name,
                ticker,
                ticker,
                lastPurchuaseQuantity,
                0,
                0,
                0,
                0,
                asOfDate,
                true,
                "Missing historical buy price. Excluded from account unrealized totals."
            );
        }

        var weightedAverageCostPerUnit = totalBoughtCost / totalBoughtUnits;
        var costBasis = lastPurchuaseQuantity * weightedAverageCostPerUnit;

        var currentPrice = await stockPriceProvider.GetPricePerUnitAsync(ticker, currency, asOfDate);
        if (currentPrice <= 0)
        {
            return new UnrealizedGainLossInstrumentResult(
                account.AccountId,
                account.Name,
                ticker,
                ticker,
                lastPurchuaseQuantity,
                costBasis,
                0,
                -costBasis,
                -100,
                asOfDate,
                true,
                "Missing current price. Excluded from account unrealized totals."
            );
        }

        var currentValue = lastPurchuaseQuantity * currentPrice;
        var unrealized = currentValue - costBasis;
        var unrealizedPercent = costBasis == 0 ? 0 : unrealized / costBasis * 100;

        return new UnrealizedGainLossInstrumentResult(
            account.AccountId,
            account.Name,
            ticker,
            ticker,
            lastPurchuaseQuantity,
            costBasis,
            currentValue,
            unrealized,
            unrealizedPercent,
            asOfDate,
            false,
            null
        );
    }
}