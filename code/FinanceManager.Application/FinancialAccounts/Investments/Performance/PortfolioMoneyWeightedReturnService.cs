using FinanceManager.Application.FinancialAccounts.Bond.Valuation;
using FinanceManager.Application.FinancialAccounts.Shared;
using FinanceManager.Domain.Assets.Services;
using FinanceManager.Domain.FinancialAccounts.Bond.Entities;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Currencies.Services;
using FinanceManager.Domain.FinancialAccounts.Investments.Entities;
using FinanceManager.Domain.FinancialAccounts.Investments.Repositories;
using FinanceManager.Domain.FinancialAccounts.Shared.Repositories;
using FinanceManager.Domain.FinancialAccounts.Shared.Services;
using FinanceManager.Domain.MoneyFlow.Entities;
using FinanceManager.Domain.MoneyFlow.Services;

namespace FinanceManager.Application.FinancialAccounts.Investments.Performance;

/// <summary>
/// Builds the portfolio cash-flow timeline used by the money-weighted return card. The current
/// investment model represents external investment cash impact with Buy/Sell rows (the semantics
/// established by #729); bond ValueChange rows provide the equivalent unit movement. Prices and FX
/// are resolved through the same application services used by the portfolio valuation paths.
/// </summary>
public class PortfolioMoneyWeightedReturnService(
    IFinancialAccountRepository financialAccountRepository,
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
        if (start == default || end == default || endDate < startDate)
            return Result(null, MoneyWeightedReturnStatus.InsufficientData, startDate, endDate);

        var accounts = await PortfolioAccounts.LoadAsync(
            financialAccountRepository, userId, startDate, endDate, cancellationToken);
        if (accounts.IsEmpty)
            return Result(null, MoneyWeightedReturnStatus.InsufficientData, startDate, endDate);

        var bondAccounts = accounts.BondAccounts;
        var investmentAccountIds = accounts.InvestmentAccountIds;
        var transactionFlows = investmentAccountIds.Length == 0
            ? []
            : await investmentTransactionRepository.GetCapitalFlowInputs(
                investmentAccountIds,
                DateOnly.FromDateTime(endDate),
                cancellationToken);

        var bondDetails = await accounts.LoadBondDetailsAsync(bondDashboardContext, cancellationToken);
        if (!accounts.HasAllBondDetails(bondDetails))
            return Result(null, MoneyWeightedReturnStatus.Unavailable, startDate, endDate);

        var movements = PortfolioPeriodLedger.Build(transactionFlows, bondAccounts, bondDetails, startDate, endDate).Movements;
        var ratesByCurrency = await PortfolioRates.LoadAsync(
            currencyExchangeService,
            movements.Select(flow => flow.Currency).Concat(bondDetails.Values.Select(details => details.Currency.ShortName)),
            currency,
            startDate,
            endDate,
            cancellationToken);

        if (!TryConvertFlows(movements, currency, ratesByCurrency, out var cashFlows))
            return Result(null, MoneyWeightedReturnStatus.Unavailable, startDate, endDate);

        var openingHoldings = investmentAccountIds.Length == 0 || startDate == DateTime.MinValue.Date
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
            return Result(null, MoneyWeightedReturnStatus.Unavailable, startDate, endDate);

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
                    throw new InvalidOperationException($"Bond valuation requires details for bond id {entry.BondDetailsId}.");

                var value = entry.GetPriceAt(valuationDate, details);
                if (value == 0m && entry.Value != 0m)
                    return (0m, false);
                if (!TryConvert(value, details.Currency.ShortName, valuationDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc), targetCurrency, ratesByCurrency, out var converted))
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