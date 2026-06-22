using FinanceManager.Domain.FinancialAccounts.Bond.Entities;
using FinanceManager.Domain.FinancialAccounts.Bond.Repositories;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Investments.Repositories;
using FinanceManager.Domain.FinancialAccounts.Shared.Repositories;
using FinanceManager.Domain.FinancialAccounts.Shared.Services;
using FinanceManager.Domain.FinancialAccounts.Stock.Entities;
using FinanceManager.Domain.FinancialAccounts.Stock.Repositories;
using FinanceManager.Domain.Identity.Entities;
using FinanceManager.Domain.Identity.Repositories;
using FinanceManager.Domain.Identity.Services;
using FinanceManager.Domain.Insights.Entities;
using FinanceManager.Domain.Insights.Services;
using FinanceManager.Domain.MoneyFlow.Entities;

namespace FinanceManager.Application.Insights.Diversification;

public class DiversificationService(
    IFinancialAccountRepository financialAccountRepository,
    IStockDetailsRepository stockDetailsRepository,
    IBondDetailsRepository bondDetailsRepository,
    IInvestmentTransactionRepository investmentTransactionRepository) : IDiversificationService
{
    private const int _totalSupportedClasses = 6;
    private const int _holdingsBenchmark = 30;

    public async Task<DiversificationScore> GetDiversificationScore(int userId, DateTime asOfDate)
    {
        var (heldAssetClasses, uniqueHoldings) = await GetCurrentHoldings(userId, asOfDate);

        var assetClassScore = CalculateAssetClassScore(heldAssetClasses.Count);
        var holdingsScore = CalculateHoldingsScore(uniqueHoldings.Count);
        var totalScore = assetClassScore + holdingsScore;

        return new DiversificationScore(totalScore, assetClassScore, holdingsScore, GetBand(totalScore));
    }

    public async Task<DiversificationBreakdown> GetDiversificationBreakdown(int userId, DateTime asOfDate, CancellationToken cancellationToken = default)
    {
        var groups = new List<AssetClassHoldings>();

        var stockHoldings = await GetStockHoldingNames(userId, asOfDate, cancellationToken);
        if (stockHoldings.Count > 0)
            groups.Add(new AssetClassHoldings("Stocks", stockHoldings));

        var bondHoldings = await GetBondHoldingNames(userId, asOfDate, cancellationToken);
        if (bondHoldings.Count > 0)
            groups.Add(new AssetClassHoldings("Bonds", bondHoldings));

        if (await HasAnyCash(userId, asOfDate))
            groups.Add(new AssetClassHoldings("Cash", ["Cash"]));

        return new DiversificationBreakdown(groups);
    }

    private async Task<List<string>> GetStockHoldingNames(int userId, DateTime asOfDate, CancellationToken cancellationToken)
    {
        var isins = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        await foreach (var account in financialAccountRepository.GetAccounts<StockAccount>(userId, DateTime.MinValue, asOfDate))
            foreach (var isin in GetCurrentlyHeldTickers(account, asOfDate))
                if (seen.Add(isin))
                    isins.Add(isin);

        var tickerByIsin = (await stockDetailsRepository.GetAll(cancellationToken))
            .Where(details => !string.IsNullOrWhiteSpace(details.Isin))
            .GroupBy(details => details.Isin, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First().Ticker, StringComparer.OrdinalIgnoreCase);

        var names = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var isin in isins)
            names.Add(tickerByIsin.TryGetValue(isin, out var ticker) && !string.IsNullOrWhiteSpace(ticker) ? ticker : isin);

        // New asset-model holdings (investment accounts) are reported under the same "Stocks" group.
        foreach (var ticker in await GetHeldInvestmentTickers(userId, asOfDate, cancellationToken))
            names.Add(ticker);

        return [.. names];
    }

    // Tickers of investment-account listings (new asset model) with a positive net holding as of the date.
    private async Task<List<string>> GetHeldInvestmentTickers(int userId, DateTime asOfDate, CancellationToken cancellationToken)
    {
        var transactions = await investmentTransactionRepository.GetByUser(userId, DateOnly.MinValue, DateOnly.FromDateTime(asOfDate), cancellationToken);

        return [.. transactions
            .GroupBy(t => t.AssetListingId)
            .Where(g => g.Sum(t => t.SignedQuantity) > 0)
            .Select(g => g.OrderByDescending(t => t.TradeDate).ThenByDescending(t => t.Id).First().AssetListing?.Ticker)
            .Where(ticker => !string.IsNullOrWhiteSpace(ticker))
            .Select(ticker => ticker!)
            .Distinct(StringComparer.OrdinalIgnoreCase)];
    }

    private async Task<List<string>> GetBondHoldingNames(int userId, DateTime asOfDate, CancellationToken cancellationToken)
    {
        var bondIds = new List<int>();
        var seen = new HashSet<int>();

        await foreach (var account in financialAccountRepository.GetAccounts<BondAccount>(userId, DateTime.MinValue, asOfDate))
            foreach (var bondId in GetCurrentlyHeldBondIds(account, asOfDate))
                if (seen.Add(bondId))
                    bondIds.Add(bondId);

        if (bondIds.Count == 0) return [];

        var names = new List<string>();
        foreach (var bondId in bondIds)
        {
            var details = await bondDetailsRepository.GetByIdAsync(bondId, cancellationToken);
            names.Add(details is not null && !string.IsNullOrWhiteSpace(details.Name) ? details.Name : $"Bond #{bondId}");
        }

        names.Sort(StringComparer.OrdinalIgnoreCase);
        return names;
    }

    private async Task<bool> HasAnyCash(int userId, DateTime asOfDate)
    {
        await foreach (var account in financialAccountRepository.GetAccounts<CurrencyAccount>(userId, DateTime.MinValue, asOfDate))
            if (HasCurrentCash(account, asOfDate))
                return true;

        return false;
    }

    private async Task<(HashSet<InvestmentType> AssetClasses, HashSet<string> Holdings)> GetCurrentHoldings(int userId, DateTime asOfDate)
    {
        var heldClasses = new HashSet<InvestmentType>();
        var uniqueHoldings = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Legacy stock holdings (resolved to display tickers) and new investment-account holdings are
        // merged and deduplicated by GetStockHoldingNames, so an instrument held under both models is
        // counted once toward the Stocks class.
        foreach (var ticker in await GetStockHoldingNames(userId, asOfDate, CancellationToken.None))
        {
            uniqueHoldings.Add($"stock_{ticker}");
            heldClasses.Add(InvestmentType.Stock);
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
        (int)(distinctClassCount / (double)_totalSupportedClasses * 50);

    private static int CalculateHoldingsScore(int uniqueTickerCount) =>
        (int)(Math.Min(uniqueTickerCount / (double)_holdingsBenchmark, 1.0) * 50);
}