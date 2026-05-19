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
    private static readonly HashSet<InvestmentType> SupportedClasses =
    [
        InvestmentType.Cash,
        InvestmentType.Stock,
        InvestmentType.Bond,
        InvestmentType.Property,
        InvestmentType.Crypto,
        InvestmentType.Commodities,
    ];

    private const int TotalSupportedClasses = 6;
    private const int HoldingsBenchmark = 30;

    public async Task<DiversificationScore> GetDiversificationScore(int userId, DateTime asOfDate)
    {
        var distinctClasses = new HashSet<InvestmentType>();
        var uniqueTickers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        await foreach (var account in financialAccountRepository.GetAccounts<StockAccount>(userId, DateTime.MinValue, asOfDate))
        {
            foreach (var ticker in account.GetStoredTickers())
                uniqueTickers.Add(ticker);

            foreach (var type in account.GetStoredTypes())
                if (SupportedClasses.Contains(type))
                    distinctClasses.Add(type);
        }

        await foreach (var account in financialAccountRepository.GetAccounts<BondAccount>(userId, DateTime.MinValue, asOfDate))
        {
            if (account.Entries.Count == 0) continue;

            distinctClasses.Add(InvestmentType.Bond);
            foreach (var entry in account.Entries)
                uniqueTickers.Add($"bond_{entry.BondDetailsId}");
        }

        await foreach (var account in financialAccountRepository.GetAccounts<CurrencyAccount>(userId, DateTime.MinValue, asOfDate))
        {
            if (account.Entries.Count == 0) continue;

            distinctClasses.Add(InvestmentType.Cash);
            uniqueTickers.Add($"cash_{account.AccountId}");
        }

        var assetClassScore = CalculateAssetClassScore(distinctClasses.Count);
        var holdingsScore = CalculateHoldingsScore(uniqueTickers.Count);
        var totalScore = assetClassScore + holdingsScore;

        var band = totalScore switch
        {
            <= 33 => "Limited",
            <= 66 => "Moderate",
            _ => "Broad"
        };

        return new DiversificationScore(totalScore, assetClassScore, holdingsScore, band);
    }

    private static int CalculateAssetClassScore(int distinctClassCount) =>
        (int)(distinctClassCount / (double)TotalSupportedClasses * 50);

    private static int CalculateHoldingsScore(int uniqueTickerCount) =>
        (int)(Math.Min(uniqueTickerCount / (double)HoldingsBenchmark, 1.0) * 50);
}
