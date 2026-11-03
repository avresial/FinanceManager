using FinanceManager.Application.Alerts.Models;
using FinanceManager.Domain.Alerts.Entities;
using FinanceManager.Domain.Alerts.Enums;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using System.Globalization;

namespace FinanceManager.Application.Alerts.Services.Conditions;

public sealed class LargeTransactionConditionEvaluator : IAlertConditionEvaluator
{
    public AlertType AlertType => AlertType.LargeTransaction;

    public AlertConditionResult Evaluate(FinancialAlert alert, AlertEvaluationSnapshot snapshot)
    {
        var inv = CultureInfo.InvariantCulture;
        var (startDate, endDate) = AlertPeriod.GetRange(alert.EvaluationPeriod, snapshot.EvaluationDate);

        // Historical import protection: for AllTime alerts, do not fire on transactions posted before alert creation
        var effectiveStart = startDate;
        if (alert.EvaluationPeriod == AlertEvaluationPeriod.AllTime && alert.CreatedAt != default)
        {
            effectiveStart = alert.CreatedAt.Date;
        }

        if (alert.EvaluationPeriod == AlertEvaluationPeriod.AllTime
            && snapshot.AllTimeEvaluationData.TryGetValue(alert.Id, out var allTimeData))
        {
            if (allTimeData.LargestTransaction is not CurrencyAccountEntry largestTransaction)
                return NoMatch(alert);

            var largestAmount = Math.Abs(largestTransaction.ValueChange);
            var fingerprint = $"LargeTransaction:Count={allTimeData.TransactionCount}:Top={largestTransaction.AccountId}:{largestTransaction.EntryId}:Max={largestAmount.ToString("F2", inv)}:Op={alert.ComparisonOperator}:Threshold={alert.Threshold.ToString("F2", inv)}";
            var matchingEntries = allTimeData.MatchingTransactions ?? [largestTransaction];

            return Triggered(largestTransaction, largestAmount, fingerprint, allTimeData.TransactionCount, matchingEntries, snapshot);
        }

        var qualifyingEntries = snapshot.GetAllEntries()
            .Where(e => e.PostingDate >= effectiveStart && e.PostingDate <= endDate)
            .Where(e => alert.AccountId is not int accId || e.AccountId == accId)
            .Where(e => e.ValueChange < 0)
            .Where(e => AlertComparison.Matches(Math.Abs(e.ValueChange), alert.ComparisonOperator, alert.Threshold))
            .OrderByDescending(e => Math.Abs(e.ValueChange))
            .ThenByDescending(e => e.PostingDate)
            .ToList();

        if (qualifyingEntries.Count > 0)
        {
            var largest = qualifyingEntries[0];
            var largestAmount = Math.Abs(largest.ValueChange);
            var entryIds = string.Join(",", qualifyingEntries.Select(e => $"{e.AccountId}:{e.EntryId}").Order());
            var fingerprint = $"LargeTransaction:Entries={entryIds}:Max={largestAmount.ToString("F2", inv)}:Op={alert.ComparisonOperator}:Threshold={alert.Threshold.ToString("F2", inv)}";

            return Triggered(largest, largestAmount, fingerprint, qualifyingEntries.Count, qualifyingEntries, snapshot);
        }

        return NoMatch(alert);
    }

    private static AlertConditionResult Triggered(
        CurrencyAccountEntry largest,
        decimal largestAmount,
        string fingerprint,
        int transactionCount,
        IEnumerable<CurrencyAccountEntry> matchingEntries,
        AlertEvaluationSnapshot snapshot)
    {
        var inv = CultureInfo.InvariantCulture;
        var context = new Dictionary<string, string>
        {
            ["TriggeringAccountId"] = largest.AccountId.ToString(inv),
            ["TriggeringEntryId"] = largest.EntryId.ToString(inv),
            ["Amount"] = largestAmount.ToString("F2", inv),
            ["PostingDate"] = largest.PostingDate.ToString("yyyy-MM-dd"),
            ["ContractorDetails"] = largest.ContractorDetails ?? string.Empty,
            ["Description"] = largest.Description,
            ["TransactionCount"] = transactionCount.ToString(inv)
        };

        return new AlertConditionResult(
            true,
            largestAmount,
            fingerprint,
            $"Large transaction detected on {largest.PostingDate:yyyy-MM-dd}",
            context,
            AlertTransactionReferences.From(matchingEntries, snapshot.IncludeAllMatchingTransactions),
            transactionCount);
    }

    private static AlertConditionResult NoMatch(FinancialAlert alert) => new(
        false,
        0m,
        $"LargeTransaction:NoMatch:Op={alert.ComparisonOperator}:Threshold={alert.Threshold.ToString("F2", CultureInfo.InvariantCulture)}",
        "No transaction currently matches the condition.",
        new Dictionary<string, string>());
}