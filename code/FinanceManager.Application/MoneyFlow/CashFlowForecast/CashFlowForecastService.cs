using FinanceManager.Application.FinancialAccounts.Shared;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Currencies.Services;
using FinanceManager.Domain.Identity.Services;
using FinanceManager.Domain.Labels.Entities;
using FinanceManager.Domain.Labels.Services;
using FinanceManager.Domain.MoneyFlow.Entities;
using FinanceManager.Domain.MoneyFlow.Services;
using FinanceManager.Domain.Shared.Services;
using FinanceManager.Domain.Transactions.Cash.Repositories;
using FinanceManager.Domain.Transactions.Shared;
using FinanceManager.Domain.Transactions.Shared.Dtos;

using ForecastModel = FinanceManager.Domain.MoneyFlow.Entities.CashFlowForecast;

namespace FinanceManager.Application.MoneyFlow.CashFlowForecast;

/// <summary>
/// Builds a deterministic, read-only cash projection from the owner's pooled cash balance and
/// strongly recurring cash movements. No forecast point is persisted as a transaction.
/// </summary>
public sealed class CashFlowForecastService(
    ICashTransactionReader cashTransactionReader,
    IRecurringTransactionDetectorService recurringTransactionDetectorService,
    ICurrencyExchangeService currencyExchangeService,
    IDateTimeProvider dateTimeProvider) : ICashFlowForecastService
{
    private const int _historyDays = 30;

    public async Task<ForecastModel> GetForecast(
        int userId,
        Currency currency,
        int horizonDays,
        CancellationToken cancellationToken = default)
    {
        if (!CashFlowForecastHorizons.IsSupported(horizonDays))
            throw new ArgumentOutOfRangeException(nameof(horizonDays), "The forecast horizon must be 30, 60, or 90 days.");

        // Keep the current partial day: chart bucket dates must not truncate the entry read to midnight.
        var asOf = dateTimeProvider.UtcNow;
        var asOfDate = asOf.Date;
        var historyStart = asOfDate.AddDays(-_historyDays);
        var cashRange = await cashTransactionReader.GetValueRange(userId, historyStart, asOf, cancellationToken);
        // The pooled cash balance and the recurring detector results are summed as stored (the application's base
        // denomination), so value both from PLN into the requested currency.
        var rates = await CurrencyRateSeries.LoadAsync(
            currencyExchangeService,
            DefaultCurrency.PLN,
            currency,
            historyStart,
            asOfDate,
            cancellationToken);
        if (rates.Count == 0 && !string.Equals(
                DefaultCurrency.PLN.ShortName,
                currency.ShortName,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"No exchange rates are available from {DefaultCurrency.PLN.ShortName} to {currency.ShortName} for the forecast period.");
        }

        var historicalSeries = BuildHistoricalSeries(cashRange, rates, historyStart, asOfDate);
        var currentBalance = historicalSeries.Count == 0 ? 0m : historicalSeries[^1].Value;

        var recurringFlows = await recurringTransactionDetectorService.GetRecurringCashFlows(
            userId,
            asOfDate,
            cancellationToken);
        // Future FX rates are unknowable, so project recurring amounts with the latest carried rate
        // available on the forecast's as-of date.
        var expectedTransactions = CurrencyRateSeries.TryGet(rates, asOfDate, out var currentRate)
            ? BuildExpectedTransactions(recurringFlows, currentRate, asOfDate, horizonDays)
            : [];
        var forecastSeries = BuildForecastSeries(currentBalance, asOfDate, horizonDays, expectedTransactions);

        return new ForecastModel
        {
            UserId = userId,
            CurrencyId = currency.Id,
            Currency = currency.ShortName,
            AsOfDate = asOfDate,
            HorizonDays = horizonDays,
            HasForecastableActivity = expectedTransactions.Count > 0,
            HistoricalSeries = historicalSeries,
            ForecastSeries = forecastSeries,
            ExpectedTransactions = expectedTransactions
        };
    }

    private static List<TimeSeriesModel> BuildHistoricalSeries(
        OwnerLedgerRange cashRange,
        IReadOnlyDictionary<DateTime, decimal> rates,
        DateTime start,
        DateTime end)
    {
        var walk = new OwnerLedgerWalk(cashRange);
        return [.. walk.EachDayEnd(start, end).Select(day => new TimeSeriesModel(
            day.Day,
            CurrencyRateSeries.TryGet(rates, day.Day, out var rate) ? Math.Round(day.Positions.Sum(position => position.Value) * rate, 2) : 0m))];
    }

    private static List<CashFlowForecastTransaction> BuildExpectedTransactions(
        IEnumerable<RecurringCashFlow> recurringFlows,
        decimal exchangeRate,
        DateTime asOfDate,
        int horizonDays)
    {
        var endDate = asOfDate.AddDays(horizonDays);
        var result = new List<CashFlowForecastTransaction>();

        foreach (var flow in recurringFlows)
        {
            if (flow.IsMuted || flow.IsCancelled || flow.NextExpectedDate.Date <= asOfDate.Date)
                continue;

            var nextDate = flow.NextExpectedDate.Date;
            while (nextDate <= endDate)
            {
                result.Add(new CashFlowForecastTransaction
                {
                    Date = nextDate,
                    Description = flow.Name,
                    Amount = Math.Round(flow.OccurrenceAmount * exchangeRate, 2),
                    Cadence = flow.Cadence,
                    PatternId = flow.PatternId
                });

                var followingDate = AddCadence(nextDate, flow.Cadence);
                if (followingDate <= nextDate)
                    break;
                nextDate = followingDate;
            }
        }

        return result
            .OrderBy(transaction => transaction.Date)
            .ThenBy(transaction => transaction.Description, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static List<TimeSeriesModel> BuildForecastSeries(
        decimal currentBalance,
        DateTime asOfDate,
        int horizonDays,
        IReadOnlyCollection<CashFlowForecastTransaction> expectedTransactions)
    {
        var changesByDate = expectedTransactions
            .GroupBy(transaction => transaction.Date.Date)
            .ToDictionary(group => group.Key, group => group.Sum(transaction => transaction.Amount));
        var result = new List<TimeSeriesModel>(horizonDays + 1)
        {
            new(asOfDate, Math.Round(currentBalance, 2))
        };
        var balance = currentBalance;

        for (var offset = 1; offset <= horizonDays; offset++)
        {
            var date = asOfDate.AddDays(offset);
            if (changesByDate.TryGetValue(date.Date, out var change))
                balance += change;

            result.Add(new TimeSeriesModel(date, Math.Round(balance, 2)));
        }

        return result;
    }

    private static DateTime AddCadence(DateTime date, RecurringCadence cadence) =>
        cadence switch
        {
            RecurringCadence.Weekly => date.AddDays(7),
            RecurringCadence.Monthly => date.AddMonths(1),
            RecurringCadence.Quarterly => date.AddMonths(3),
            RecurringCadence.Annual => date.AddYears(1),
            _ => date
        };
}