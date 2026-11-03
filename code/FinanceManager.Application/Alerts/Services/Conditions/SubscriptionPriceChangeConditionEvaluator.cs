using FinanceManager.Application.Alerts.Models;
using FinanceManager.Domain.Alerts.Entities;
using FinanceManager.Domain.Alerts.Enums;
using FinanceManager.Domain.Labels.Entities;
using System.Globalization;

namespace FinanceManager.Application.Alerts.Services.Conditions;

public sealed class SubscriptionPriceChangeConditionEvaluator : IAlertConditionEvaluator
{
    public AlertType AlertType => AlertType.SubscriptionPriceChange;

    public AlertConditionResult Evaluate(FinancialAlert alert, AlertEvaluationSnapshot snapshot)
    {
        var inv = CultureInfo.InvariantCulture;
        var targetSubs = snapshot.Subscriptions.AsEnumerable();
        if (alert.SubscriptionId is Guid subId)
        {
            targetSubs = targetSubs.Where(s => s.PatternId == subId);
        }

        var qualifyingSubs = new List<(RecurringTransactionResult Sub, decimal PriceDelta)>();

        foreach (var sub in targetSubs)
        {
            if (sub.PreviousAmount <= 0m || sub.LastAmount == sub.PreviousAmount)
                continue;

            var delta = sub.PriceDelta;

            var matches = alert.Threshold switch
            {
                0m when alert.ComparisonOperator == AlertComparisonOperator.GreaterThan => delta > 0m,
                0m when alert.ComparisonOperator == AlertComparisonOperator.NotEqual => delta != 0m,
                _ => AlertComparison.Matches(delta, alert.ComparisonOperator, alert.Threshold)
            };

            if (matches)
            {
                qualifyingSubs.Add((sub, delta));
            }
        }

        if (qualifyingSubs.Count > 0)
        {
            var (topSub, delta) = qualifyingSubs
                .OrderByDescending(x => Math.Abs(x.PriceDelta))
                .First();

            var subKeys = string.Join(",", qualifyingSubs.Select(s => $"{s.Sub.PatternId}:{s.Sub.PreviousAmount.ToString("F2", inv)}->{s.Sub.LastAmount.ToString("F2", inv)}:{s.Sub.LastChargeDate:yyyyMMdd}").Order());
            var fingerprint = $"SubscriptionPriceChange:Subs={subKeys}";
            var message = $"Subscription '{topSub.Name}' price changed from {topSub.PreviousAmount.ToString("N2", inv)} to {topSub.LastAmount.ToString("N2", inv)}";

            var context = new Dictionary<string, string>
            {
                ["PatternId"] = topSub.PatternId.ToString(),
                ["SubscriptionName"] = topSub.Name,
                ["PreviousAmount"] = topSub.PreviousAmount.ToString("F2", inv),
                ["LastAmount"] = topSub.LastAmount.ToString("F2", inv),
                ["PriceDelta"] = delta.ToString("F2", inv),
                ["LastChargeDate"] = topSub.LastChargeDate.ToString("yyyy-MM-dd")
            };

            return new AlertConditionResult(true, delta, fingerprint, message, context, OccurrenceCount: qualifyingSubs.Count);
        }

        var defaultFingerprint = $"SubscriptionPriceChange:NoMatch:Op={alert.ComparisonOperator}:Threshold={alert.Threshold.ToString("F2", inv)}";
        var defaultMessage = "No subscription price change currently matches the condition.";

        return new AlertConditionResult(false, 0m, defaultFingerprint, defaultMessage, new Dictionary<string, string>());
    }
}