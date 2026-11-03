using FinanceManager.Application.Alerts.Models;
using FinanceManager.Application.Alerts.Services.Conditions;
using FinanceManager.Domain.Alerts.Entities;
using FinanceManager.Domain.Alerts.Enums;

namespace FinanceManager.Application.Alerts.Services;

/// <summary>
/// Turns a raw condition result into an alert outcome: disabled, healthy, suppressed (unchanged trigger),
/// newly triggered, or error. The condition itself is delegated to the <see cref="IAlertConditionEvaluator"/>
/// registered for the alert's type.
/// </summary>
public class FinancialAlertEvaluator(IEnumerable<IAlertConditionEvaluator> conditionEvaluators) : IFinancialAlertEvaluator
{
    private readonly Dictionary<AlertType, IAlertConditionEvaluator> _conditionEvaluators =
        conditionEvaluators.ToDictionary(evaluator => evaluator.AlertType);

    public AlertEvaluationOutcome Evaluate(FinancialAlert alert, AlertEvaluationSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(alert);
        ArgumentNullException.ThrowIfNull(snapshot);

        var evaluationTime = snapshot.EvaluationDate;

        if (!alert.IsEnabled)
        {
            return new AlertEvaluationOutcome(
                alert.Id,
                alert.Title,
                alert.AlertType,
                AlertTriggerStatus.Disabled,
                IsTriggered: false,
                TriggeredAt: null,
                IsSuppressed: true,
                DeDuplicationReason.AlertDisabled,
                CurrentValue: 0m,
                alert.Threshold,
                alert.ComparisonOperator,
                ConditionFingerprint: string.Empty,
                Message: $"Alert '{alert.Title}' is disabled.",
                EvaluatedAt: evaluationTime,
                Context: new Dictionary<string, string>());
        }

        try
        {
            if (!_conditionEvaluators.TryGetValue(alert.AlertType, out var conditionEvaluator))
                throw new NotSupportedException($"Alert type '{alert.AlertType}' is not supported.");

            var condition = conditionEvaluator.Evaluate(alert, snapshot);

            if (!condition.ConditionMet)
            {
                return FromCondition(alert, condition, evaluationTime, AlertTriggerStatus.Healthy,
                    isTriggered: false, triggeredAt: null, isSuppressed: false, DeDuplicationReason.None);
            }

            // Condition is met. Suppress only an unchanged condition; changed transactions trigger
            // a fresh evaluation immediately.
            if (alert.IsUnchangedTrigger(condition.Fingerprint))
            {
                return FromCondition(alert, condition, evaluationTime, AlertTriggerStatus.Triggered,
                    isTriggered: true, triggeredAt: null, isSuppressed: true, DeDuplicationReason.UnchangedCondition);
            }

            // Newly triggered
            return FromCondition(alert, condition, evaluationTime, AlertTriggerStatus.Triggered,
                isTriggered: true, triggeredAt: evaluationTime, isSuppressed: false, DeDuplicationReason.None);
        }
        catch (Exception ex)
        {
            return new AlertEvaluationOutcome(
                alert.Id,
                alert.Title,
                alert.AlertType,
                AlertTriggerStatus.Error,
                IsTriggered: false,
                TriggeredAt: null,
                IsSuppressed: false,
                DeDuplicationReason.None,
                CurrentValue: 0m,
                alert.Threshold,
                alert.ComparisonOperator,
                ConditionFingerprint: string.Empty,
                Message: $"Evaluation failed: {ex.Message}",
                EvaluatedAt: evaluationTime,
                Context: new Dictionary<string, string>(),
                ErrorMessage: ex.ToString());
        }
    }

    public IReadOnlyList<AlertEvaluationOutcome> EvaluateAll(
        IEnumerable<FinancialAlert> alerts,
        AlertEvaluationSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(alerts);
        ArgumentNullException.ThrowIfNull(snapshot);

        var outcomes = new List<AlertEvaluationOutcome>();
        foreach (var alert in alerts)
        {
            outcomes.Add(Evaluate(alert, snapshot));
        }

        return outcomes;
    }

    private static AlertEvaluationOutcome FromCondition(
        FinancialAlert alert,
        AlertConditionResult condition,
        DateTime evaluationTime,
        AlertTriggerStatus status,
        bool isTriggered,
        DateTime? triggeredAt,
        bool isSuppressed,
        DeDuplicationReason reason) => new(
            alert.Id,
            alert.Title,
            alert.AlertType,
            status,
            isTriggered,
            triggeredAt,
            isSuppressed,
            reason,
            condition.ObservedValue,
            alert.Threshold,
            alert.ComparisonOperator,
            condition.Fingerprint,
            condition.Message,
            evaluationTime,
            condition.Context,
            MatchingTransactions: condition.MatchingTransactions,
            MatchingTransactionCount: condition.MatchingTransactionCount,
            OccurrenceCount: condition.OccurrenceCount ?? condition.MatchingTransactionCount);
}