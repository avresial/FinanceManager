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
/// Builds daily portfolio valuation periods for the time-weighted return calculation. The current
/// investment model represents external investment cash impact with Buy/Sell rows (the semantics
/// established by #729); bond ValueChange rows provide the equivalent unit movement.
/// </summary>
public class PortfolioTimeWeightedReturnService(
    IFinancialAccountRepository financialAccountRepository,
    IInvestmentTransactionRepository investmentTransactionRepository,
    IInvestmentPriceProvider investmentPriceProvider,
    BondDashboardContext bondDashboardContext,
    ICurrencyExchangeService currencyExchangeService) : ITimeWeightedReturnService
{
    public async Task<TimeWeightedReturnResult> GetAsync(
        int userId,
        Currency currency,
        DateTime start,
        DateTime end,
        CancellationToken cancellationToken = default)
    {
        var startDate = start.Date;
        var endDate = (end > DateTime.UtcNow ? DateTime.UtcNow : end).Date;
        if (start == default || end == default || endDate < startDate)
            return Result(null, TimeWeightedReturnStatus.InsufficientData, startDate, endDate);

        var accounts = await PortfolioAccounts.LoadAsync(
            financialAccountRepository, userId, startDate, endDate, cancellationToken);
        if (accounts.IsEmpty)
            return Result(null, TimeWeightedReturnStatus.InsufficientData, startDate, endDate);

        var bondAccounts = accounts.BondAccounts;
        var investmentAccountIds = accounts.InvestmentAccountIds;
        IReadOnlyList<IInvestmentTransactionRepository.CapitalFlowInput> transactionFlows = investmentAccountIds.Length == 0
            ? []
            : await investmentTransactionRepository.GetCapitalFlowInputs(
                investmentAccountIds,
                DateOnly.FromDateTime(endDate),
                cancellationToken);
        var valuationInputs = investmentAccountIds.Length == 0
            ? new IInvestmentTransactionRepository.AccountValuationInputs([], [])
            : await investmentTransactionRepository.GetValuationInputs(
                investmentAccountIds,
                DateOnly.FromDateTime(startDate),
                DateOnly.FromDateTime(endDate),
                cancellationToken);

        var bondDetails = await accounts.LoadBondDetailsAsync(bondDashboardContext, cancellationToken);
        if (!accounts.HasAllBondDetails(bondDetails))
            return Result(null, TimeWeightedReturnStatus.Unavailable, startDate, endDate);

        var pendingFlows = BuildPendingFlows(transactionFlows, bondAccounts, bondDetails, startDate, endDate);
        var ratesByCurrency = await PortfolioRates.LoadAsync(
            currencyExchangeService,
            pendingFlows.Select(flow => flow.Currency).Concat(bondDetails.Values.Select(details => details.Currency.ShortName)),
            currency,
            startDate,
            endDate,
            cancellationToken);
        if (!TryConvertFlows(pendingFlows, currency, ratesByCurrency, out var externalFlowsByDate))
            return Result(null, TimeWeightedReturnStatus.Unavailable, startDate, endDate);

        try
        {
            var investmentValues = await PortfolioDailyValuation.BuildInvestmentValuesAsync(
                investmentPriceProvider,
                valuationInputs,
                currency,
                startDate,
                endDate,
                cancellationToken);
            if (!investmentValues.Complete)
                return Result(null, TimeWeightedReturnStatus.Unavailable, startDate, endDate);

            var bondValues = PortfolioDailyValuation.BuildBondValues(
                bondAccounts,
                bondDetails,
                ratesByCurrency,
                currency,
                startDate,
                endDate);
            if (!bondValues.Complete)
                return Result(null, TimeWeightedReturnStatus.Unavailable, startDate, endDate);

            var periods = BuildPeriods(
                investmentValues,
                bondValues,
                externalFlowsByDate,
                startDate,
                endDate);
            return TimeWeightedReturnCalculator.Calculate(periods, startDate, endDate);
        }
        catch (Exception ex) when (ex is InvalidOperationException or FormatException)
        {
            return Result(null, TimeWeightedReturnStatus.Unavailable, startDate, endDate);
        }
    }

    private static List<TimeWeightedReturnPeriod> BuildPeriods(
        PortfolioValuationSeries investmentValues,
        PortfolioValuationSeries bondValues,
        IReadOnlyDictionary<DateTime, decimal> externalFlowsByDate,
        DateTime startDate,
        DateTime endDate)
    {
        List<TimeWeightedReturnPeriod> periods = [];
        var startingValue = investmentValues.OpeningValue + bondValues.OpeningValue;

        for (var date = startDate; date <= endDate; date = date.AddDays(1))
        {
            var endingValue = investmentValues.Values.GetValueOrDefault(date)
                              + bondValues.Values.GetValueOrDefault(date);
            var externalFlow = externalFlowsByDate.GetValueOrDefault(date);
            if (startingValue == 0m && externalFlow == 0m && endingValue == 0m)
            {
                startingValue = endingValue;
                continue;
            }

            periods.Add(new(date, startingValue, externalFlow, endingValue));
            startingValue = endingValue;
        }

        return periods;
    }

    private static List<PendingFlow> BuildPendingFlows(
        IReadOnlyList<IInvestmentTransactionRepository.CapitalFlowInput> transactionFlows,
        IReadOnlyList<BondAccount> bondAccounts,
        IReadOnlyDictionary<int, BondDetails> bondDetails,
        DateTime startDate,
        DateTime endDate)
    {
        List<PendingFlow> result = [];

        foreach (var flow in transactionFlows)
        {
            var date = flow.TradeDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            if (date < startDate
                || date > endDate
                || flow.Quantity <= 0m
                || flow.UnitPrice < 0m
                || flow.Type is not (InvestmentTransactionType.Buy or InvestmentTransactionType.Sell))
                continue;

            var sourceCurrency = flow.Currency.Trim();
            var isMinorQuote = DefaultCurrency.MinorQuoteUnits.TryGetValue(sourceCurrency, out var majorCurrency);
            var multiplier = isMinorQuote ? 0.01m : 1m;
            var amount = flow.Quantity * flow.UnitPrice * multiplier;
            var fee = flow.Fee ?? 0m;
            amount = flow.Type == InvestmentTransactionType.Buy ? amount + fee : amount - fee;
            if (amount == 0m) continue;

            result.Add(new PendingFlow(
                date,
                flow.Type == InvestmentTransactionType.Buy ? amount : -amount,
                isMinorQuote ? majorCurrency! : sourceCurrency,
                flow.TransactionId));
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

                result.Add(new PendingFlow(
                    DateTime.SpecifyKind(date, DateTimeKind.Utc),
                    entry.ValueChange * details.UnitValue,
                    details.Currency.ShortName,
                    entry.EntryId));
            }
        }

        return result;
    }

    private static bool TryConvertFlows(
        IEnumerable<PendingFlow> pendingFlows,
        Currency targetCurrency,
        IReadOnlyDictionary<string, IReadOnlyDictionary<DateTime, decimal>> ratesByCurrency,
        out Dictionary<DateTime, decimal> externalFlowsByDate)
    {
        externalFlowsByDate = [];
        foreach (var flow in pendingFlows.OrderBy(x => x.Date).ThenBy(x => x.Sequence))
        {
            if (!PortfolioDailyValuation.TryConvert(flow.Amount, flow.Currency, flow.Date, targetCurrency, ratesByCurrency, out var amount))
            {
                externalFlowsByDate.Clear();
                return false;
            }

            externalFlowsByDate[flow.Date.Date] = externalFlowsByDate.GetValueOrDefault(flow.Date.Date) + amount;
        }

        return true;
    }

    private static TimeWeightedReturnResult Result(
        decimal? totalReturn,
        TimeWeightedReturnStatus status,
        DateTime startDate,
        DateTime endDate) =>
        new(totalReturn, status, startDate, endDate);

    private sealed record PendingFlow(DateTime Date, decimal Amount, string Currency, long Sequence);
}