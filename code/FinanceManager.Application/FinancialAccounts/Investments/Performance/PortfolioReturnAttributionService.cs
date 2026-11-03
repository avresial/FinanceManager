using FinanceManager.Application.FinancialAccounts.Bond.Valuation;
using FinanceManager.Application.FinancialAccounts.Shared;
using FinanceManager.Domain.Assets.Services;
using FinanceManager.Domain.FinancialAccounts.Bond.Entities;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Currencies.Services;
using FinanceManager.Domain.FinancialAccounts.Investments.Entities;
using FinanceManager.Domain.FinancialAccounts.Investments.Repositories;
using FinanceManager.Domain.FinancialAccounts.Shared.Repositories;
using FinanceManager.Domain.MoneyFlow.Entities;
using FinanceManager.Domain.MoneyFlow.Services;

namespace FinanceManager.Application.FinancialAccounts.Investments.Performance;

/// <summary>
/// Attributes the selected portfolio value change without overlapping components. Buy and sell
/// rows are the current investment model's external cash-impact representation established by #729;
/// known transaction fees are separated, and the remaining market/valuation effect is calculated as
/// the reconciliation residual after currency translation.
/// </summary>
public class PortfolioReturnAttributionService(
    IFinancialAccountRepository financialAccountRepository,
    IInvestmentTransactionRepository investmentTransactionRepository,
    IInvestmentPriceProvider investmentPriceProvider,
    BondDashboardContext bondDashboardContext,
    ICurrencyExchangeService currencyExchangeService) : IPortfolioReturnAttributionService
{
    public async Task<PortfolioReturnAttributionResult> GetAsync(
        int userId,
        Currency currency,
        DateTime start,
        DateTime end,
        CancellationToken cancellationToken = default)
    {
        var startDate = start.Date;
        var endDate = (end > DateTime.UtcNow ? DateTime.UtcNow : end).Date;
        if (start == default || end == default || endDate < startDate)
            return PortfolioReturnAttributionResult.InsufficientData(startDate, endDate);

        var accounts = await PortfolioAccounts.LoadAsync(
            financialAccountRepository, bondDashboardContext, userId, startDate, endDate, cancellationToken);
        if (accounts.IsEmpty)
            return PortfolioReturnAttributionResult.InsufficientData(startDate, endDate);

        var bondAccounts = accounts.BondAccounts;
        var bondDetails = accounts.BondDetails;
        var investmentAccountIds = accounts.InvestmentAccountIds;
        var transactionFlows = investmentAccountIds.Length == 0
            ? []
            : await investmentTransactionRepository.GetCapitalFlowInputs(
                investmentAccountIds,
                DateOnly.FromDateTime(endDate),
                cancellationToken);

        if (!TryBuildListingCurrencies(transactionFlows, out var listingCurrencies))
            return PortfolioReturnAttributionResult.Unavailable(startDate, endDate);

        if (!accounts.HasAllBondDetails)
            return PortfolioReturnAttributionResult.Unavailable(startDate, endDate);

        var ledger = PortfolioPeriodLedger.Build(transactionFlows, bondAccounts, bondDetails, startDate, endDate);
        var ratesByCurrency = await PortfolioRates.LoadAsync(
            currencyExchangeService,
            listingCurrencies.Values
                .Concat(bondDetails.Values.Select(details => PortfolioRates.NormalizeCurrency(details.Currency.ShortName))),
            currency,
            startDate,
            endDate,
            cancellationToken);

        if (!TryConvertFlows(ledger.Movements, currency, ratesByCurrency, out var externalCashMovement, out var feeEffect))
            return PortfolioReturnAttributionResult.Unavailable(startDate, endDate);

        var openingHoldings = investmentAccountIds.Length == 0
            ? new Dictionary<int, IReadOnlyDictionary<long, decimal>>()
            : await investmentTransactionRepository.GetHoldingsByAccountAsOf(
                investmentAccountIds,
                DateOnly.FromDateTime(startDate).AddDays(-1),
                cancellationToken);
        var endingHoldings = investmentAccountIds.Length == 0
            ? new Dictionary<int, IReadOnlyDictionary<long, decimal>>()
            : await investmentTransactionRepository.GetHoldingsByAccountAsOf(
                investmentAccountIds,
                DateOnly.FromDateTime(endDate),
                cancellationToken);

        var prices = new PortfolioPriceCache(investmentPriceProvider, currency);
        var (openingStockValue, openingStockComplete) = await prices.GetHoldingsValueAsync(openingHoldings, startDate, cancellationToken);
        var (endingStockValue, endingStockComplete) = await prices.GetHoldingsValueAsync(endingHoldings, endDate, cancellationToken);
        if (!openingStockComplete || !endingStockComplete)
            return PortfolioReturnAttributionResult.Unavailable(startDate, endDate);

        var (investmentFxEffect, investmentFxComplete) = await PortfolioFxEffectCalculator.GetInvestmentEffectAsync(
            ledger.QuantityChanges,
            openingHoldings,
            listingCurrencies,
            startDate,
            endDate,
            ratesByCurrency,
            prices,
            cancellationToken);
        if (!investmentFxComplete)
            return PortfolioReturnAttributionResult.Unavailable(startDate, endDate);

        var (openingBondValue, openingBondComplete) = GetBondValue(
            bondAccounts,
            bondDetails,
            ratesByCurrency,
            currency,
            DateOnly.FromDateTime(startDate).AddDays(-1),
            DateOnly.FromDateTime(startDate));
        var (endingBondValue, endingBondComplete) = GetBondValue(
            bondAccounts,
            bondDetails,
            ratesByCurrency,
            currency,
            DateOnly.FromDateTime(endDate),
            DateOnly.FromDateTime(endDate));
        if (!openingBondComplete || !endingBondComplete)
            return PortfolioReturnAttributionResult.Unavailable(startDate, endDate);

        if (!PortfolioFxEffectCalculator.TryGetBondEffect(
                bondAccounts,
                bondDetails,
                currency,
                startDate,
                endDate,
                ratesByCurrency,
                out var bondFxEffect))
            return PortfolioReturnAttributionResult.Unavailable(startDate, endDate);

        var fxEffect = investmentFxEffect + bondFxEffect;
        var totalChange = openingStockValue + openingBondValue;
        var endingValue = endingStockValue + endingBondValue;
        totalChange = endingValue - totalChange;
        var marketEffect = totalChange - externalCashMovement - feeEffect - fxEffect;

        return PortfolioReturnAttributionResult.Available(
            totalChange,
            externalCashMovement,
            marketEffect,
            feeEffect,
            fxEffect,
            startDate,
            endDate);
    }

    private static bool TryBuildListingCurrencies(
        IReadOnlyList<IInvestmentTransactionRepository.CapitalFlowInput> transactionFlows,
        out Dictionary<long, string> listingCurrencies)
    {
        listingCurrencies = new Dictionary<long, string>();
        foreach (var flow in transactionFlows)
        {
            if (flow.Quantity <= 0m || flow.UnitPrice < 0m || flow.Type is not (InvestmentTransactionType.Buy or InvestmentTransactionType.Sell))
                continue;

            var sourceCurrency = PortfolioRates.NormalizeCurrency(flow.Currency);
            if (string.IsNullOrWhiteSpace(sourceCurrency))
                return false;

            if (listingCurrencies.TryGetValue(flow.AssetListingId, out var existingCurrency)
                && !string.Equals(existingCurrency, sourceCurrency, StringComparison.OrdinalIgnoreCase))
                return false;

            listingCurrencies[flow.AssetListingId] = sourceCurrency;
        }

        return true;
    }

    private static bool TryConvertFlows(
        IEnumerable<PortfolioMovement> movements,
        Currency targetCurrency,
        IReadOnlyDictionary<string, IReadOnlyDictionary<DateTime, decimal>> ratesByCurrency,
        out decimal externalCashMovement,
        out decimal feeEffect)
    {
        externalCashMovement = 0m;
        feeEffect = 0m;
        foreach (var flow in movements.OrderBy(x => x.Date).ThenBy(x => x.SourceId))
        {
            var signedAmount = flow.Kind is PortfolioMovementKind.Sale or PortfolioMovementKind.BondWithdrawal or PortfolioMovementKind.Fee
                ? -flow.Amount : flow.Amount;
            if (!TryConvert(signedAmount, flow.Currency, flow.Date, targetCurrency, ratesByCurrency, out var converted))
                return false;
            if (flow.Kind is PortfolioMovementKind.Fee or PortfolioMovementKind.FeeRebate)
                feeEffect += converted;
            else
                externalCashMovement += converted;
        }

        return true;
    }

    private static (decimal Value, bool Complete) GetBondValue(
        IEnumerable<BondAccount> accounts,
        IReadOnlyDictionary<int, BondDetails> bondDetails,
        IReadOnlyDictionary<string, IReadOnlyDictionary<DateTime, decimal>> ratesByCurrency,
        Currency targetCurrency,
        DateOnly entryAsOfDate,
        DateOnly valuationDate)
    {
        decimal total = 0m;
        foreach (var account in accounts)
        {
            var entries = account.Entries
                .Concat(account.NextOlderEntries.Values)
                .Where(entry => DateOnly.FromDateTime(entry.PostingDate) <= entryAsOfDate)
                .GroupBy(entry => entry.BondDetailsId)
                .Select(group => group.OrderByDescending(entry => entry.PostingDate).ThenByDescending(entry => entry.EntryId).First());

            foreach (var entry in entries)
            {
                if (!bondDetails.TryGetValue(entry.BondDetailsId, out var details))
                    return (0m, false);

                if (!PortfolioFxEffectCalculator.TryGetBondPrice(entry, details, valuationDate, out var value))
                    return (0m, false);
                if (value == 0m && entry.Value != 0m)
                    return (0m, false);

                var sourceCurrency = PortfolioRates.NormalizeCurrency(details.Currency.ShortName);
                if (!TryConvert(
                        value,
                        sourceCurrency,
                        valuationDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
                        targetCurrency,
                        ratesByCurrency,
                        out var converted))
                    return (0m, false);

                total += converted;
            }
        }

        return (total, true);
    }

    private static bool TryConvert(
        decimal amount,
        string sourceCurrency,
        DateTime date,
        Currency targetCurrency,
        IReadOnlyDictionary<string, IReadOnlyDictionary<DateTime, decimal>> ratesByCurrency,
        out decimal converted)
    {
        if (amount == 0m || string.Equals(sourceCurrency, targetCurrency.ShortName, StringComparison.OrdinalIgnoreCase))
        {
            converted = amount;
            return true;
        }

        if (ratesByCurrency.TryGetValue(sourceCurrency, out var rates)
            && CurrencyRateSeries.TryGet(rates, date, out var rate)
            && rate > 0m)
        {
            converted = amount * rate;
            return true;
        }

        converted = 0m;
        return false;
    }
}