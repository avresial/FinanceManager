using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Shared.Repositories;
using FinanceManager.Domain.Identity.Services;
using FinanceManager.Domain.Labels.Entities;
using FinanceManager.Domain.Labels.Services;
using FinanceManager.Domain.MoneyFlow.Entities;
using FinanceManager.Domain.MoneyFlow.Services;
using FinanceManager.Domain.Shared.Services;

using ForecastModel = FinanceManager.Domain.MoneyFlow.Entities.CashFlowForecast;

namespace FinanceManager.Application.MoneyFlow.CashFlowForecast;

/// <summary>
/// Builds a deterministic, read-only cash projection from the latest account balances and
/// strongly recurring cash movements. No forecast point is persisted as a transaction.
/// </summary>
public sealed class CashFlowForecastService(
    IFinancialAccountRepository financialAccountRepository,
    IRecurringTransactionDetectorService recurringTransactionDetectorService,
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

        var asOfDate = dateTimeProvider.TodayUtc;
        var historyStart = asOfDate.AddDays(-_historyDays);
        var accounts = await LoadCashAccounts(userId, historyStart, asOfDate, cancellationToken);
        var historicalSeries = BuildHistoricalSeries(accounts, historyStart, asOfDate);
        var currentBalance = historicalSeries.Count == 0 ? 0m : historicalSeries[^1].Value;

        var recurringFlows = await recurringTransactionDetectorService.GetRecurringCashFlows(userId, cancellationToken);
        var expectedTransactions = BuildExpectedTransactions(recurringFlows, asOfDate, horizonDays);
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

    private async Task<List<CurrencyAccount>> LoadCashAccounts(
        int userId,
        DateTime start,
        DateTime end,
        CancellationToken cancellationToken)
    {
        var accounts = new List<CurrencyAccount>();
        await foreach (var account in financialAccountRepository.GetAccounts<CurrencyAccount>(userId, start, end))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (account is not null)
                accounts.Add(account);
        }

        return accounts;
    }

    private static List<TimeSeriesModel> BuildHistoricalSeries(
        IReadOnlyCollection<CurrencyAccount> accounts,
        DateTime start,
        DateTime end)
    {
        var balances = InitializeSeries(start, end);

        foreach (var account in accounts)
        {
            var entries = account.Entries
                .OrderBy(entry => entry.PostingDate)
                .ThenBy(entry => entry.EntryId)
                .ToList();
            var runningBalance = account.NextOlderEntry?.Value ?? 0m;
            var entryIndex = 0;

            while (entryIndex < entries.Count && entries[entryIndex].PostingDate.Date < start.Date)
            {
                runningBalance = entries[entryIndex].Value;
                entryIndex++;
            }

            for (var date = start.Date; date <= end.Date; date = date.AddDays(1))
            {
                while (entryIndex < entries.Count && entries[entryIndex].PostingDate.Date <= date)
                {
                    runningBalance = entries[entryIndex].Value;
                    entryIndex++;
                }

                balances[date] += runningBalance;
            }
        }

        return balances
            .Select(point => new TimeSeriesModel(point.Key, Math.Round(point.Value, 2)))
            .ToList();
    }

    private static List<CashFlowForecastTransaction> BuildExpectedTransactions(
        IEnumerable<RecurringCashFlow> recurringFlows,
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
                    Amount = Math.Round(flow.OccurrenceAmount, 2),
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

    private static Dictionary<DateTime, decimal> InitializeSeries(DateTime start, DateTime end)
    {
        var result = new Dictionary<DateTime, decimal>();
        for (var date = start.Date; date <= end.Date; date = date.AddDays(1))
            result[date] = 0m;
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