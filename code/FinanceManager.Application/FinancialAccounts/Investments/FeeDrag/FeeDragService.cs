using FinanceManager.Domain.Assets.Entities;
using FinanceManager.Domain.Assets.Services;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Investments.Dtos;
using FinanceManager.Domain.FinancialAccounts.Investments.Entities;
using FinanceManager.Domain.FinancialAccounts.Investments.Repositories;
using FinanceManager.Domain.FinancialAccounts.Investments.Services;
using FinanceManager.Domain.FinancialAccounts.Shared.Repositories;

namespace FinanceManager.Application.FinancialAccounts.Investments.FeeDrag;

/// <summary>
/// Calculates the annual and long-term cost of known ETF expense ratios from current holdings.
/// The query is read-only: holdings are reconstructed from investment transactions and prices are
/// resolved through the existing investment price provider.
/// </summary>
internal sealed class FeeDragService(
    IAccountRepository<InvestmentAccount> accountRepository,
    IInvestmentTransactionRepository transactionRepository,
    IInvestmentPriceProvider priceProvider) : IFeeDragService
{
    private const decimal _defaultReturnRate = 0.07m;
    private static readonly int[] _projectionHorizons = [10, 20, 30];

    public async Task<FeeDragAnalysisResult> GetAnalysisAsync(
        int userId,
        Currency currency,
        DateTime asOfDate,
        decimal assumedAnnualReturnRate = _defaultReturnRate,
        CancellationToken cancellationToken = default)
    {
        ValidateReturnRate(assumedAnnualReturnRate);

        var result = new FeeDragAnalysisResult
        {
            AssumedAnnualReturnRate = assumedAnnualReturnRate,
        };

        if (userId <= 0 || currency is null)
        {
            result.Projections = BuildProjections(0m, 0m, assumedAnnualReturnRate);
            return result;
        }

        var asOf = asOfDate == default ? DateTime.UtcNow : asOfDate;
        var asOfDateOnly = DateOnly.FromDateTime(asOf);
        var accounts = await accountRepository.GetAll(userId);
        var positions = new Dictionary<long, HoldingPosition>();

        foreach (var account in accounts.Where(account => account.UserId == userId))
        {
            var transactions = await transactionRepository.GetByAccount(account.AccountId, cancellationToken);

            foreach (var listingTransactions in transactions
                         .Where(transaction => transaction.TradeDate <= asOfDateOnly)
                         .GroupBy(transaction => transaction.AssetListingId))
            {
                var quantity = listingTransactions.Sum(transaction => transaction.SignedQuantity);
                if (quantity <= 0m)
                    continue;

                var listing = listingTransactions
                    .Select(transaction => transaction.AssetListing)
                    .FirstOrDefault(candidate => candidate?.Asset is not null);

                if (listing?.Asset is not { Type: AssetType.ETF })
                    continue;

                if (positions.TryGetValue(listing.Id, out var existing))
                {
                    existing.Quantity += quantity;
                }
                else
                {
                    positions.Add(listing.Id, new HoldingPosition(listing, quantity));
                }
            }
        }

        decimal knownValue = 0m;
        decimal weightedExpenseRatioValue = 0m;

        foreach (var position in positions.Values.OrderBy(position => position.Listing.Ticker, StringComparer.OrdinalIgnoreCase))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var price = await priceProvider.GetPricePerUnitAsync(position.Listing.Id, currency, asOf, cancellationToken);
            if (price <= 0m)
                continue;

            var value = position.Quantity * price;
            if (value <= 0m)
                continue;

            var expenseRatio = position.Listing.Asset.TotalExpenseRatio is decimal ratio && ratio >= 0m
                ? ratio
                : (decimal?)null;
            var annualFeeCost = expenseRatio is decimal knownRatio ? value * knownRatio : 0m;

            result.TotalHoldingsValue += value;
            result.TotalHoldingsCount++;
            result.Holdings.Add(new FeeDragHolding
            {
                ListingId = position.Listing.Id,
                Ticker = position.Listing.Ticker ?? string.Empty,
                Name = position.Listing.Asset.Name ?? string.Empty,
                Value = value,
                ExpenseRatio = expenseRatio,
                AnnualFeeCost = annualFeeCost,
            });

            if (expenseRatio is decimal knownExpenseRatio)
            {
                knownValue += value;
                weightedExpenseRatioValue += value * knownExpenseRatio;
                result.AnnualFeeCost += annualFeeCost;
            }
            else
            {
                result.MissingTerCount++;
                result.MissingTerHoldingsValue += value;
            }
        }

        result.TotalHoldingsValue = RoundMoney(result.TotalHoldingsValue);
        result.AnnualFeeCost = RoundMoney(result.AnnualFeeCost);
        result.MissingTerHoldingsValue = RoundMoney(result.MissingTerHoldingsValue);

        var weightedExpenseRatio = knownValue > 0m ? weightedExpenseRatioValue / knownValue : 0m;
        result.WeightedExpenseRatio = weightedExpenseRatio;
        result.Projections = BuildProjections(knownValue, weightedExpenseRatio, assumedAnnualReturnRate);
        return result;
    }

    internal static void ValidateReturnRate(decimal assumedAnnualReturnRate)
    {
        if (assumedAnnualReturnRate is < IFeeDragService.MinimumReturnRate or > IFeeDragService.MaximumReturnRate)
        {
            throw new ArgumentOutOfRangeException(
                nameof(assumedAnnualReturnRate),
                assumedAnnualReturnRate,
                $"The assumed annual return rate must be between {IFeeDragService.MinimumReturnRate:P0} and {IFeeDragService.MaximumReturnRate:P0}.");
        }
    }

    internal static List<FeeDragProjection> BuildProjections(
        decimal principal,
        decimal weightedExpenseRatio,
        decimal assumedAnnualReturnRate)
    {
        var netRate = Math.Max(-1m, assumedAnnualReturnRate - weightedExpenseRatio);

        return _projectionHorizons
            .Select(years =>
            {
                var grossFutureValue = principal * (decimal)Math.Pow((double)(1m + assumedAnnualReturnRate), years);
                var netFutureValue = principal * (decimal)Math.Pow((double)(1m + netRate), years);
                var roundedGross = RoundMoney(grossFutureValue);
                var roundedNet = RoundMoney(netFutureValue);

                return new FeeDragProjection
                {
                    Years = years,
                    GrossFutureValue = roundedGross,
                    NetFutureValue = roundedNet,
                    CumulativeFeeCost = RoundMoney(Math.Max(0m, roundedGross - roundedNet)),
                };
            })
            .ToList();
    }

    private static decimal RoundMoney(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);

    private sealed class HoldingPosition(AssetListing listing, decimal quantity)
    {
        public AssetListing Listing { get; } = listing;
        public decimal Quantity { get; set; } = quantity;
    }
}