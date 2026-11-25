using FinanceManager.Application.Currencies.ExchangeRates;
using FinanceManager.Application.Transactions.Bonds.Valuation;
using FinanceManager.Domain.Assets.Services;
using FinanceManager.Domain.Currencies.Entities;
using FinanceManager.Domain.Currencies.Services;
using FinanceManager.Domain.MoneyFlow.Entities;
using FinanceManager.Domain.MoneyFlow.Services;
using FinanceManager.Domain.Transactions.Bonds.Repositories;
using FinanceManager.Domain.Transactions.Investments.Entities;
using FinanceManager.Domain.Transactions.Investments.Repositories;

namespace FinanceManager.Application.Portfolio.Performance;

/// <summary>
/// Attributes the selected portfolio value change without overlapping components. Buy and sell
/// rows are the current investment model's external cash-impact representation established by #729;
/// known transaction fees are separated, and the remaining market/valuation effect is calculated as
/// the reconciliation residual after currency translation.
/// </summary>
public class PortfolioReturnAttributionService(
    IBondTransactionReader bondTransactionReader,
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
        // Guest users have negative ids; only the unset id 0 is invalid.
        if (userId == 0 || start == default || end == default || endDate < startDate)
            return PortfolioReturnAttributionResult.InsufficientData(startDate, endDate);

        var transactionFlows = await investmentTransactionRepository.GetCapitalFlowInputsByUser(
            userId,
            DateOnly.FromDateTime(endDate),
            cancellationToken);
        var bonds = await PortfolioBondInputs.LoadAsync(
            bondTransactionReader, bondDashboardContext, userId, startDate, endDate, cancellationToken);
        if (transactionFlows.Count == 0 && bonds.IsEmpty)
            return PortfolioReturnAttributionResult.InsufficientData(startDate, endDate);

        if (!TryBuildListingCurrencies(transactionFlows, out var listingCurrencies))
            return PortfolioReturnAttributionResult.Unavailable(startDate, endDate);

        if (!bonds.HasAllDetails)
            return PortfolioReturnAttributionResult.Unavailable(startDate, endDate);

        var ledger = PortfolioPeriodLedger.Build(transactionFlows, bonds.Range.Points, bonds.Details, startDate, endDate);
        var ratesByCurrency = await PortfolioRates.LoadAsync(
            currencyExchangeService,
            listingCurrencies.Values
                .Concat(bonds.Details.Values.Select(details => PortfolioRates.NormalizeCurrency(details.Currency.ShortName))),
            currency,
            startDate,
            endDate,
            cancellationToken);

        if (!TryConvertFlows(ledger.Movements, currency, ratesByCurrency, out var externalCashMovement, out var feeEffect))
            return PortfolioReturnAttributionResult.Unavailable(startDate, endDate);

        var openingHoldings = startDate == DateTime.MinValue.Date
            ? new Dictionary<long, decimal>()
            : await PortfolioHoldings.LoadAsync(
                investmentTransactionRepository, userId, DateOnly.FromDateTime(startDate).AddDays(-1), cancellationToken);
        var endingHoldings = await PortfolioHoldings.LoadAsync(
            investmentTransactionRepository, userId, DateOnly.FromDateTime(endDate), cancellationToken);

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

        if (!bonds.TryValue(bonds.Range.Opening, ratesByCurrency, currency, DateOnly.FromDateTime(startDate), out var openingBondValue)
            || !bonds.TryValue(bonds.EndingPositions, ratesByCurrency, currency, DateOnly.FromDateTime(endDate), out var endingBondValue))
            return PortfolioReturnAttributionResult.Unavailable(startDate, endDate);

        if (!PortfolioFxEffectCalculator.TryGetBondEffect(
                bonds,
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
        IReadOnlyList<IInvestmentTransactionRepository.UserCapitalFlowInput> transactionFlows,
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