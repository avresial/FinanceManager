using FinanceManager.Application.Alerts.Models;
using FinanceManager.Domain.Alerts.Entities;
using FinanceManager.Domain.Alerts.Enums;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;

namespace FinanceManager.Application.Alerts.Services.Conditions;

/// <summary>
/// Outgoing spend in the alert's period, shared by the category and merchant spending conditions.
/// All-time alerts reuse pre-aggregated totals from the snapshot when they are available.
/// </summary>
internal sealed record AlertSpendingTotals(
    decimal TotalSpend,
    int TransactionCount,
    IReadOnlyList<CurrencyAccountEntry> MatchingEntries,
    string PeriodKey)
{
    public static AlertSpendingTotals Compute(
        FinancialAlert alert,
        AlertEvaluationSnapshot snapshot,
        Func<CurrencyAccountEntry, bool> matchesTarget)
    {
        var (startDate, endDate) = AlertPeriod.GetRange(alert.EvaluationPeriod, snapshot.EvaluationDate);
        var periodKey = AlertPeriod.FormatKey(alert.EvaluationPeriod, startDate, endDate);

        if (alert.EvaluationPeriod == AlertEvaluationPeriod.AllTime
            && snapshot.AllTimeEvaluationData.TryGetValue(alert.Id, out var allTimeData))
        {
            return new AlertSpendingTotals(
                allTimeData.TotalSpend,
                allTimeData.TransactionCount,
                allTimeData.MatchingTransactions ?? [],
                periodKey);
        }

        var qualifyingEntries = snapshot.GetAllEntries()
            .Where(e => e.PostingDate >= startDate && e.PostingDate <= endDate)
            .Where(e => alert.AccountId is not int accId || e.AccountId == accId)
            .Where(e => e.ValueChange < 0)
            .Where(matchesTarget)
            .ToList();

        return new AlertSpendingTotals(
            qualifyingEntries.Sum(e => Math.Abs(e.ValueChange)),
            qualifyingEntries.Count,
            qualifyingEntries,
            periodKey);
    }
}