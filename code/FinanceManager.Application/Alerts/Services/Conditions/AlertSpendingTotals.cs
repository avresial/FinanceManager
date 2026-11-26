using FinanceManager.Application.Alerts.Models;
using FinanceManager.Domain.Alerts.Entities;
using FinanceManager.Domain.Alerts.Enums;
using FinanceManager.Domain.Transactions.Cash.Dtos;

namespace FinanceManager.Application.Alerts.Services.Conditions;

/// <summary>
/// Outgoing spend in the alert's period and currency, shared by the category and merchant spending conditions.
/// All-time alerts reuse pre-aggregated totals from the snapshot when they are available.
/// </summary>
internal sealed record AlertSpendingTotals(
    int CurrencyId,
    decimal TotalSpend,
    int TransactionCount,
    IReadOnlyList<CashLogEntry> MatchingEntries,
    string PeriodKey)
{
    public static AlertSpendingTotals Compute(
        FinancialAlert alert,
        AlertEvaluationSnapshot snapshot,
        Func<CashLogEntry, bool> matchesTarget)
    {
        var currencyId = AlertCurrency.Require(alert);
        var (startDate, endDate) = AlertPeriod.GetRange(alert.EvaluationPeriod, snapshot.EvaluationDate);
        var periodKey = AlertPeriod.FormatKey(alert.EvaluationPeriod, startDate, endDate);

        if (alert.EvaluationPeriod == AlertEvaluationPeriod.AllTime
            && snapshot.AllTimeEvaluationData.TryGetValue(alert.Id, out var allTimeData))
        {
            return new AlertSpendingTotals(
                currencyId,
                allTimeData.TotalSpend,
                allTimeData.TransactionCount,
                allTimeData.MatchingTransactions ?? [],
                periodKey);
        }

        var qualifyingEntries = snapshot.RecentEntries
            .Where(e => e.PostingDate >= startDate && e.PostingDate <= endDate)
            .Where(e => e.CurrencyId == currencyId)
            .Where(e => e.ValueChange < 0)
            .Where(matchesTarget)
            .ToList();

        return new AlertSpendingTotals(
            currencyId,
            qualifyingEntries.Sum(e => Math.Abs(e.ValueChange)),
            qualifyingEntries.Count,
            qualifyingEntries,
            periodKey);
    }
}