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
        var heldAssetClasses = await GetHeldAssetClasses(userId, asOfDate);
        var uniqueHoldings = await GetUniqueHoldingsPerType(userId, asOfDate);

        var assetClassScore = CalculateAssetClassScore(heldAssetClasses.Count);
        var holdingsScore = CalculateHoldingsScore(uniqueHoldings.Count);
        var totalScore = assetClassScore + holdingsScore;

        return new DiversificationScore(totalScore, assetClassScore, holdingsScore, GetBand(totalScore));
    }

    private async Task<HashSet<InvestmentType>> GetHeldAssetClasses(int userId, DateTime asOfDate)
    {
        var heldClasses = new HashSet<InvestmentType>();

        var hasStocks = await financialAccountRepository.GetAccounts<StockAccount>(userId, DateTime.MinValue, asOfDate)
            .AnyAsync(account => account.Entries.Count > 0);
        if (hasStocks) heldClasses.Add(InvestmentType.Stock);

        var hasBonds = await financialAccountRepository.GetAccounts<BondAccount>(userId, DateTime.MinValue, asOfDate)
            .AnyAsync(account => account.Entries.Count > 0);
        if (hasBonds) heldClasses.Add(InvestmentType.Bond);

        var hasCash = await financialAccountRepository.GetAccounts<CurrencyAccount>(userId, DateTime.MinValue, asOfDate)
            .AnyAsync(account => account.ContainsAssets);
        if (hasCash) heldClasses.Add(InvestmentType.Cash);

        return heldClasses;
    }

    private async Task<HashSet<string>> GetUniqueHoldingsPerType(int userId, DateTime asOfDate)
    {
        var uniqueTickers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        await foreach (var account in financialAccountRepository.GetAccounts<StockAccount>(userId, DateTime.MinValue, asOfDate))
            foreach (var ticker in account.GetStoredTickers())
                uniqueTickers.Add($"stock_{ticker}");

        await foreach (var account in financialAccountRepository.GetAccounts<BondAccount>(userId, DateTime.MinValue, asOfDate))
            foreach (var entry in account.Entries)
                uniqueTickers.Add($"bond_{entry.BondDetailsId}");

        var hasCash = await financialAccountRepository.GetAccounts<CurrencyAccount>(userId, DateTime.MinValue, asOfDate)
            .AnyAsync(account => account.ContainsAssets);
        if (hasCash) uniqueTickers.Add("cash");

        return uniqueTickers;
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
