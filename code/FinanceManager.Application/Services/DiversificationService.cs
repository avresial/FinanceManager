using FinanceManager.Domain.Entities.Bonds;
using FinanceManager.Domain.Entities.FinancialAccounts.Currencies;
using FinanceManager.Domain.Entities.MoneyFlowModels;
using FinanceManager.Domain.Entities.Stocks;
using FinanceManager.Domain.Enums;
using FinanceManager.Domain.Repositories.Account;
using FinanceManager.Domain.Services;

namespace FinanceManager.Application.Services;

public class DiversificationService(IFinancialAccountRepository financialAccountRepository) : IDiversificationService
{
    private const int TotalSupportedClasses = 6;
    private const int HoldingsBenchmark = 30;

    public async Task<DiversificationScore> GetDiversificationScore(int userId, DateTime asOfDate)
    {
        var (heldAssetClasses, uniqueHoldings) = await GetCurrentHoldings(userId, asOfDate);

        var assetClassScore = CalculateAssetClassScore(heldAssetClasses.Count);
        var holdingsScore = CalculateHoldingsScore(uniqueHoldings.Count);
        var totalScore = assetClassScore + holdingsScore;

        return new DiversificationScore(totalScore, assetClassScore, holdingsScore, GetBand(totalScore));
    }

    private async Task<(HashSet<InvestmentType> AssetClasses, HashSet<string> Holdings)> GetCurrentHoldings(int userId, DateTime asOfDate)
    {
        var heldClasses = new HashSet<InvestmentType>();
        var uniqueHoldings = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        await foreach (var account in financialAccountRepository.GetAccounts<StockAccount>(userId, DateTime.MinValue, asOfDate))
        {
            foreach (var ticker in GetCurrentlyHeldTickers(account, asOfDate))
            {
                uniqueHoldings.Add($"stock_{ticker}");
                heldClasses.Add(InvestmentType.Stock);
            }
        }

        await foreach (var account in financialAccountRepository.GetAccounts<BondAccount>(userId, DateTime.MinValue, asOfDate))
        {
            foreach (var bondId in GetCurrentlyHeldBondIds(account, asOfDate))
            {
                uniqueHoldings.Add($"bond_{bondId}");
                heldClasses.Add(InvestmentType.Bond);
            }
        }

        await foreach (var account in financialAccountRepository.GetAccounts<CurrencyAccount>(userId, DateTime.MinValue, asOfDate))
        {
            if (HasCurrentCash(account, asOfDate))
            {
                uniqueHoldings.Add("cash");
                heldClasses.Add(InvestmentType.Cash);
            }
        }

        return (heldClasses, uniqueHoldings);
    }

    private static IEnumerable<string> GetCurrentlyHeldTickers(StockAccount account, DateTime asOfDate)
    {
        foreach (var ticker in account.GetStoredTickers())
        {
            var entry = account.GetThisOrNextOlder(asOfDate, ticker);
            if (entry is { InvestmentType: InvestmentType.Stock } && entry.Value > 0)
                yield return ticker;
        }
    }

    private static IEnumerable<int> GetCurrentlyHeldBondIds(BondAccount account, DateTime asOfDate)
    {
        foreach (var bondId in account.GetStoredBondsIds())
        {
            var entry = account.GetThisOrNextOlder(asOfDate, bondId);
            if (entry is not null && entry.Value > 0)
                yield return bondId;
        }
    }

    private static bool HasCurrentCash(CurrencyAccount account, DateTime asOfDate)
    {
        var entry = account.GetThisOrNextOlder(asOfDate);
        return entry is not null && entry.Value > 0;
    }

    internal static string GetBand(int totalScore) => totalScore switch
    {
        <= 33 => "Limited",
        <= 66 => "Moderate",
        _ => "Broad"
    };

    private static int CalculateAssetClassScore(int distinctClassCount) =>
        (int)(distinctClassCount / (double)TotalSupportedClasses * 50);

    private static int CalculateHoldingsScore(int uniqueTickerCount) =>
        (int)(Math.Min(uniqueTickerCount / (double)HoldingsBenchmark, 1.0) * 50);
}
