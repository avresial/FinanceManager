using FinanceManager.Application.Alerts.Models;
using FinanceManager.Domain.Alerts.Entities;
using FinanceManager.Domain.Alerts.Enums;
using System.Globalization;

namespace FinanceManager.Application.Alerts.Services.Conditions;

public sealed class MerchantSpendingConditionEvaluator : IAlertConditionEvaluator
{
    public AlertType AlertType => AlertType.MerchantSpending;

    public AlertConditionResult Evaluate(FinancialAlert alert, AlertEvaluationSnapshot snapshot)
    {
        var inv = CultureInfo.InvariantCulture;
        var merchantTarget = (alert.MerchantName ?? string.Empty).Trim();
        var totals = AlertSpendingTotals.Compute(alert, snapshot, e =>
        {
            if (string.IsNullOrWhiteSpace(merchantTarget)) return true;
            var contractor = e.ContractorDetails ?? string.Empty;
            var desc = e.Description ?? string.Empty;
            return contractor.Contains(merchantTarget, StringComparison.OrdinalIgnoreCase)
                || desc.Contains(merchantTarget, StringComparison.OrdinalIgnoreCase);
        });

        var conditionMet = AlertComparison.Matches(totals.TotalSpend, alert.ComparisonOperator, alert.Threshold);

        var fingerprint = $"MerchantSpending:Merchant={merchantTarget.ToLowerInvariant()}:Period={totals.PeriodKey}:Spend={totals.TotalSpend.ToString("F2", inv)}:Op={alert.ComparisonOperator}:Threshold={alert.Threshold.ToString("F2", inv)}";
        var message = $"Spending for merchant '{merchantTarget}' in {AlertPeriod.FormatLabel(alert.EvaluationPeriod)} is {totals.TotalSpend.ToString("N2", inv)}";

        var context = new Dictionary<string, string>
        {
            ["Merchant"] = merchantTarget,
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