using FinanceManager.Application.FinancialAccounts.Bond.Valuation;
using FinanceManager.Application.FinancialAccounts.Shared;
using FinanceManager.Domain.Assets.Services;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Currencies.Services;
using FinanceManager.Domain.FinancialAccounts.Investments.Repositories;
using FinanceManager.Domain.MoneyFlow.Entities;
using FinanceManager.Domain.MoneyFlow.Services;
using FinanceManager.Domain.Transactions.Bonds.Repositories;

namespace FinanceManager.Application.FinancialAccounts.Investments.Performance;

/// <summary>
/// Builds the portfolio cash-flow timeline used by the money-weighted return card. The current
/// investment model represents external investment cash impact with Buy/Sell rows (the semantics
/// established by #729); bond ValueChange rows provide the equivalent unit movement. Prices and FX
/// are resolved through the same application services used by the portfolio valuation paths.
/// </summary>
public class PortfolioMoneyWeightedReturnService(
    IBondTransactionReader bondTransactionReader,
    IInvestmentTransactionRepository investmentTransactionRepository,
    IInvestmentPriceProvider investmentPriceProvider,
    BondDashboardContext bondDashboardContext,
    ICurrencyExchangeService currencyExchangeService) : IMoneyWeightedReturnService
{
    public async Task<MoneyWeightedReturnResult> GetAsync(
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
            return Result(null, MoneyWeightedReturnStatus.InsufficientData, startDate, endDate);

        var transactionFlows = await investmentTransactionRepository.GetCapitalFlowInputsByUser(
            userId,
            DateOnly.FromDateTime(endDate),
            cancellationToken);
        var bonds = await PortfolioBondInputs.LoadAsync(
            bondTransactionReader, bondDashboardContext, userId, startDate, endDate, cancellationToken);
        if (transactionFlows.Count == 0 && bonds.IsEmpty)
            return Result(null, MoneyWeightedReturnStatus.InsufficientData, startDate, endDate);
        if (!bonds.HasAllDetails)
            return Result(null, MoneyWeightedReturnStatus.Unavailable, startDate, endDate);

        var movements = PortfolioPeriodLedger.Build(transactionFlows, bonds.Range.Points, bonds.Details, startDate, endDate).Movements;
        var ratesByCurrency = await PortfolioRates.LoadAsync(
            currencyExchangeService,
            movements.Select(flow => flow.Currency).Concat(bonds.Details.Values.Select(details => PortfolioRates.NormalizeCurrency(details.Currency.ShortName))),
            currency,
            startDate,
            endDate,
            cancellationToken);

        if (!TryConvertFlows(movements, currency, ratesByCurrency, out var cashFlows))
            return Result(null, MoneyWeightedReturnStatus.Unavailable, startDate, endDate);

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
            return Result(null, MoneyWeightedReturnStatus.Unavailable, startDate, endDate);

        if (!bonds.TryValue(bonds.Range.Opening, ratesByCurrency, currency, DateOnly.FromDateTime(startDate), out var openingBondValue)
            || !bonds.TryValue(bonds.EndingPositions, ratesByCurrency, currency, DateOnly.FromDateTime(endDate), out var endingBondValue))
            return Result(null, MoneyWeightedReturnStatus.Unavailable, startDate, endDate);

        var openingValue = openingStockValue + openingBondValue;
        if (openingValue != 0m)
            cashFlows.Add(new XirrCashFlow(startDate, -openingValue));

        var endingValue = endingStockValue + endingBondValue;
        if (endingValue != 0m)
            cashFlows.Add(new XirrCashFlow(endDate, endingValue));

        return XirrCalculator.Calculate(cashFlows, startDate, endDate);
    }

    private static bool TryConvertFlows(
        IEnumerable<PortfolioMovement> movements,
        Currency targetCurrency,
        IReadOnlyDictionary<string, IReadOnlyDictionary<DateTime, decimal>> ratesByCurrency,
        out List<XirrCashFlow> cashFlows)
    {
        cashFlows = [];
        foreach (var flow in movements.OrderBy(x => x.Date).ThenBy(x => x.SourceId))
        {
            var signedAmount = flow.Kind is PortfolioMovementKind.Sale or PortfolioMovementKind.FeeRebate or PortfolioMovementKind.BondWithdrawal
                ? flow.Amount : -flow.Amount;
            if (!TryConvert(signedAmount, flow.Currency, flow.Date, targetCurrency, ratesByCurrency, out var amount))
            {
                cashFlows.Clear();
                return false;
            }

            cashFlows.Add(new XirrCashFlow(flow.Date, amount));
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
        if (string.Equals(sourceCurrency, targetCurrency.ShortName, StringComparison.OrdinalIgnoreCase))
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

    private static MoneyWeightedReturnResult Result(
        decimal? annualizedReturn,
        MoneyWeightedReturnStatus status,
        DateTime startDate,
        DateTime endDate) =>
        new(annualizedReturn, status, startDate, endDate);
}