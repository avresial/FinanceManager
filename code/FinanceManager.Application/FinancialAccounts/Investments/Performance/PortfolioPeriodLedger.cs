using FinanceManager.Domain.FinancialAccounts.Bond.Entities;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Investments.Entities;
using FinanceManager.Domain.FinancialAccounts.Investments.Repositories;
using FinanceManager.Domain.Transactions.Shared.Dtos;

namespace FinanceManager.Application.FinancialAccounts.Investments.Performance;

/// <summary>Normalizes period movements without valuation, FX conversion, or return semantics.</summary>
public sealed record PortfolioPeriodLedger(
    IReadOnlyList<PortfolioMovement> Movements,
    IReadOnlyList<PortfolioQuantityChange> QuantityChanges)
{
    public static PortfolioPeriodLedger Build(
        IReadOnlyList<IInvestmentTransactionRepository.UserCapitalFlowInput> transactionFlows,
        IReadOnlyList<OwnerLedgerPoint> bondPoints,
        IReadOnlyDictionary<int, BondDetails> bondDetails,
        DateTime startDate,
        DateTime endDate)
    {
        List<PortfolioMovement> result = [];
        List<PortfolioQuantityChange> quantityChanges = [];

        foreach (var flow in transactionFlows)
        {
            var date = flow.TradeDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            if (date < startDate || date > endDate || flow.Quantity <= 0m || flow.UnitPrice < 0m
                || flow.Type is not (InvestmentTransactionType.Buy or InvestmentTransactionType.Sell))
                continue;

            var sourceCurrency = flow.Currency.Trim();
            var isMinorQuote = DefaultCurrency.MinorQuoteUnits.TryGetValue(sourceCurrency, out var majorCurrency);
            var multiplier = isMinorQuote ? 0.01m : 1m;
            var principal = flow.Quantity * flow.UnitPrice * multiplier;
            var currency = isMinorQuote ? majorCurrency! : sourceCurrency;
            quantityChanges.Add(new PortfolioQuantityChange(
                flow.TransactionId, flow.AssetListingId, date,
                flow.Type == InvestmentTransactionType.Buy ? PortfolioMovementKind.Purchase : PortfolioMovementKind.Sale,
                flow.Quantity));
            if (principal != 0m)
                result.Add(new PortfolioMovement(
                    flow.TransactionId, flow.AssetListingId, date,
                    flow.Type == InvestmentTransactionType.Buy ? PortfolioMovementKind.Purchase : PortfolioMovementKind.Sale,
                    principal, currency, flow.Quantity));

            var fee = flow.Fee ?? 0m;
            if (fee != 0m)
                result.Add(new PortfolioMovement(
                    flow.TransactionId, flow.AssetListingId, date,
                    fee > 0m ? PortfolioMovementKind.Fee : PortfolioMovementKind.FeeRebate,
                    Math.Abs(fee), currency));
        }

        foreach (var point in bondPoints)
        {
            var date = point.PostingDate.Date;
            if (date < startDate || date > endDate || point.ValueChange == 0m)
                continue;
            if (!bondDetails.TryGetValue(point.PartitionId, out var details))
                throw new InvalidOperationException($"Bond valuation requires details for bond id {point.PartitionId}.");

            var amount = point.ValueChange * details.UnitValue;
            if (amount == 0m)
                continue;
            result.Add(new PortfolioMovement(
                point.EntryId, point.PartitionId,
                DateTime.SpecifyKind(date, DateTimeKind.Utc),
                amount > 0m ? PortfolioMovementKind.BondContribution : PortfolioMovementKind.BondWithdrawal,
                Math.Abs(amount), details.Currency.ShortName));
        }

        return new PortfolioPeriodLedger(result, quantityChanges);
    }
}