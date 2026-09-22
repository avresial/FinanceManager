using FinanceManager.Domain.FinancialAccounts.Bond.Entities;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Investments.Entities;
using FinanceManager.Domain.FinancialAccounts.Investments.Repositories;

namespace FinanceManager.Application.FinancialAccounts.Investments.Performance;

/// <summary>Normalizes period movements without valuation, FX conversion, or return semantics.</summary>
public sealed record PortfolioPeriodLedger(
    IReadOnlyList<PortfolioMovement> Movements,
    IReadOnlyList<PortfolioQuantityChange> QuantityChanges)
{
    public static PortfolioPeriodLedger Build(
        IReadOnlyList<IInvestmentTransactionRepository.CapitalFlowInput> transactionFlows,
        IReadOnlyList<BondAccount> bondAccounts,
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
            var multiplier = isMinorQuote ? flow.ListingPriceMultiplier ?? 0.01m : 1m;
            var principal = flow.Quantity * flow.UnitPrice * multiplier;
            var currency = isMinorQuote ? majorCurrency! : sourceCurrency;
            quantityChanges.Add(new PortfolioQuantityChange(
                flow.TransactionId, flow.AccountId, flow.AssetListingId, date,
                flow.Type == InvestmentTransactionType.Buy ? PortfolioMovementKind.Purchase : PortfolioMovementKind.Sale,
                flow.Quantity));
            if (principal != 0m)
                result.Add(new PortfolioMovement(
                    flow.TransactionId, flow.AccountId, flow.AssetListingId, date,
                    flow.Type == InvestmentTransactionType.Buy ? PortfolioMovementKind.Purchase : PortfolioMovementKind.Sale,
                    principal, currency, flow.Quantity));

            var fee = flow.Fee ?? 0m;
            if (fee != 0m)
                result.Add(new PortfolioMovement(
                    flow.TransactionId, flow.AccountId, flow.AssetListingId, date,
                    fee > 0m ? PortfolioMovementKind.Fee : PortfolioMovementKind.FeeRebate,
                    Math.Abs(fee), currency));
        }

        foreach (var account in bondAccounts)
        {
            foreach (var entry in account.Entries)
            {
                var date = entry.PostingDate.Date;
                if (date < startDate || date > endDate || entry.ValueChange == 0m)
                    continue;
                if (!bondDetails.TryGetValue(entry.BondDetailsId, out var details))
                    throw new InvalidOperationException($"Bond valuation requires details for bond id {entry.BondDetailsId}.");

                var amount = entry.ValueChange * details.UnitValue;
                if (amount == 0m)
                    continue;
                result.Add(new PortfolioMovement(
                    entry.EntryId, account.AccountId, entry.BondDetailsId,
                    DateTime.SpecifyKind(date, DateTimeKind.Utc),
                    amount > 0m ? PortfolioMovementKind.BondContribution : PortfolioMovementKind.BondWithdrawal,
                    Math.Abs(amount), details.Currency.ShortName));
            }
        }

        return new PortfolioPeriodLedger(result, quantityChanges);
    }
}