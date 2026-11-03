using FinanceManager.Application.Alerts.Models;
using FinanceManager.Domain.Alerts.Entities;
using FinanceManager.Domain.Alerts.Enums;
using System.Globalization;

namespace FinanceManager.Application.Alerts.Services.Conditions;

public sealed class CategorySpendingConditionEvaluator : IAlertConditionEvaluator
{
    public AlertType AlertType => AlertType.CategorySpending;

    public AlertConditionResult Evaluate(FinancialAlert alert, AlertEvaluationSnapshot snapshot)
    {
        var inv = CultureInfo.InvariantCulture;
        var totals = AlertSpendingTotals.Compute(alert, snapshot, e =>
        {
            if (e.Labels is null || e.Labels.Count == 0) return false;
            if (alert.LabelId is int targetId)
                return e.Labels.Any(l => l.Id == targetId);
            if (!string.IsNullOrWhiteSpace(alert.LabelName))
                return e.Labels.Any(l => string.Equals(l.Name, alert.LabelName, StringComparison.OrdinalIgnoreCase));
            return true;
        });

        var labelKey = alert.LabelId?.ToString(inv) ?? alert.LabelName ?? "All";
        var conditionMet = AlertComparison.Matches(totals.TotalSpend, alert.ComparisonOperator, alert.Threshold);

        var fingerprint = $"CategorySpending:Label={labelKey}:Period={totals.PeriodKey}:Spend={totals.TotalSpend.ToString("F2", inv)}:Op={alert.ComparisonOperator}:Threshold={alert.Threshold.ToString("F2", inv)}";
        var message = $"Spending for category '{labelKey}' in {AlertPeriod.FormatLabel(alert.EvaluationPeriod)} is {totals.TotalSpend.ToString("N2", inv)}";

        var context = new Dictionary<string, string>
        {
            ["Label"] = labelKey,
            ["Period"] = totals.PeriodKey,
            ["TotalSpend"] = totals.TotalSpend.ToString("F2", inv),
            ["TransactionCount"] = totals.TransactionCount.ToString(inv)
        };

        return new AlertConditionResult(
            conditionMet,
            totals.TotalSpend,
            fingerprint,
            message,
            context,
            AlertTransactionReferences.From(totals.MatchingEntries, snapshot.IncludeAllMatchingTransactions),
            totals.TransactionCount);
    }
}