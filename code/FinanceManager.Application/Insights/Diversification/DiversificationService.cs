using FinanceManager.Domain.Currencies.Repositories;
using FinanceManager.Domain.Insights.Entities;
using FinanceManager.Domain.Insights.Services;
using FinanceManager.Domain.Transactions.Bonds.Repositories;
using FinanceManager.Domain.Transactions.Cash.Repositories;
using FinanceManager.Domain.Transactions.Investments.Repositories;

namespace FinanceManager.Application.Insights.Diversification;

/// <summary>
/// Scores diversification from what the owner currently holds, pooled per listing, bond definition and currency across all
/// accounts and unlinked entries.
/// </summary>
public class DiversificationService(
    ICashTransactionReader cashTransactionReader,
    IBondTransactionReader bondTransactionReader,
    IBondDetailsRepository bondDetailsRepository,
    IInvestmentTransactionRepository investmentTransactionRepository,
    ICurrencyRepository currencyRepository) : IDiversificationService
{
    private const int _totalSupportedClasses = 6;
    private const int _holdingsBenchmark = 30;

    public async Task<DiversificationScore> GetDiversificationScore(int userId, DateTime asOfDate)
    {
        var held = await GetCurrentHoldings(userId, asOfDate, CancellationToken.None);

        var heldClassCount = new[] { held.Tickers.Count, held.BondIds.Count, held.CashCurrencyIds.Count }.Count(count => count > 0);
        var assetClassScore = CalculateAssetClassScore(heldClassCount);
        var holdingsScore = CalculateHoldingsScore(held.Tickers.Count + held.BondIds.Count + held.CashCurrencyIds.Count);
        var totalScore = assetClassScore + holdingsScore;

        return new DiversificationScore(totalScore, assetClassScore, holdingsScore, GetBand(totalScore));
    }

    public async Task<DiversificationBreakdown> GetDiversificationBreakdown(int userId, DateTime asOfDate, CancellationToken cancellationToken = default)
    {
        var held = await GetCurrentHoldings(userId, asOfDate, cancellationToken);
        var groups = new List<AssetClassHoldings>();

        if (held.Tickers.Count > 0)
            groups.Add(new AssetClassHoldings("Stocks", held.Tickers));

        if (held.BondIds.Count > 0)
            groups.Add(new AssetClassHoldings("Bonds", await GetBondHoldingNames(held.BondIds, cancellationToken)));

        if (held.CashCurrencyIds.Count > 0)
        {
            var cashCurrencies = await GetCashCurrencyCodes(held.CashCurrencyIds, cancellationToken);
            if (cashCurrencies.Count > 0)
                groups.Add(new AssetClassHoldings("Cash", cashCurrencies));
        }

        return new DiversificationBreakdown(groups);
    }

    private async Task<HeldPositions> GetCurrentHoldings(int userId, DateTime asOfDate, CancellationToken cancellationToken)
    {
        // Guest sandbox users have negative ids, so only the unset id 0 is invalid.
        if (userId == 0) return new([], [], []);

        var holdings = await investmentTransactionRepository.GetHoldingsByUserAsOf(userId, DateOnly.FromDateTime(asOfDate), cancellationToken);
        var tickers = holdings
            .Where(holding => holding.Units > 0m && !string.IsNullOrWhiteSpace(holding.Ticker))
            .Select(holding => holding.Ticker)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var bondIds = (await bondTransactionReader.GetPositions(userId, asOfDate, cancellationToken))
            .Where(position => position.Value > 0m)
            .Select(position => position.PartitionId)
            .ToList();

        var cashCurrencyIds = (await cashTransactionReader.GetPositions(userId, asOfDate, cancellationToken))
            .Where(position => position.Value > 0m)
            .Select(position => position.PartitionId)
            .ToList();

        return new(tickers, bondIds, cashCurrencyIds);
    }

    private async Task<List<string>> GetBondHoldingNames(IReadOnlyCollection<int> bondIds, CancellationToken cancellationToken)
    {
        var namesById = await bondDetailsRepository.GetNamesByIdsAsync(bondIds, cancellationToken);
        var names = bondIds
            .Select(bondId => namesById.TryGetValue(bondId, out var name) && !string.IsNullOrWhiteSpace(name) ? name : $"Bond #{bondId}")
            .ToList();

        names.Sort(StringComparer.OrdinalIgnoreCase);
        return names;
    }

    private async Task<List<string>> GetCashCurrencyCodes(IReadOnlyCollection<int> currencyIds, CancellationToken cancellationToken)
    {
        var codes = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        await foreach (var currency in currencyRepository.GetCurrencies(cancellationToken))
        {
            if (currencyIds.Contains(currency.Id) && !string.IsNullOrWhiteSpace(currency.ShortName))
                codes.Add(currency.ShortName.ToUpperInvariant());
        }

        return [.. codes];
    }

    private sealed record HeldPositions(IReadOnlyList<string> Tickers, IReadOnlyList<int> BondIds, IReadOnlyList<int> CashCurrencyIds);

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