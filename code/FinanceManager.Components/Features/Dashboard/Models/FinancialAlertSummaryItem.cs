using FinanceManager.Application.Alerts.Models;
using FinanceManager.Domain.Alerts.Enums;

namespace FinanceManager.Components.Features.Dashboard.Models;

/// <summary>
/// One triggered alert as the Financial Alerts card summary renders it: title, comparison detail and
/// occurrence label. Fields the summary never shows (<c>EvaluatedAt</c>, fingerprint, message,
/// context, matching transactions) are deliberately absent so they cannot make an unchanged summary
/// look changed.
/// </summary>
public sealed record FinancialAlertSummaryItem(
    Guid AlertId,
    string Title,
    decimal CurrentValue,
    decimal Threshold,
    AlertComparisonOperator ComparisonOperator,
    int OccurrenceCount)
{
    public static FinancialAlertSummaryItem From(AlertEvaluationOutcome outcome) => new(
        outcome.AlertId,
        outcome.AlertTitle,
        outcome.CurrentValue,
        outcome.Threshold,
        outcome.ComparisonOperator,
        Alerts.Components.AlertPresentation.OccurrenceCount(outcome));

    /// <summary>
    /// Placeholder outcome for the summary row; carries only what the summary and the detail header
    /// render until the transient detail evaluation replaces it. Never snapshotted.
    /// </summary>
    public AlertEvaluationOutcome ToOutcome() => new(
        AlertId,
        Title,
        default,
        AlertTriggerStatus.Triggered,
        IsTriggered: true,
        TriggeredAt: null,
        IsSuppressed: false,
        DeDuplicationReason.None,
        CurrentValue,
        Threshold,
        ComparisonOperator,
        ConditionFingerprint: string.Empty,
        Message: string.Empty,
        EvaluatedAt: default,
        Context: new Dictionary<string, string>(),
        MatchingTransactionCount: OccurrenceCount,
        OccurrenceCount: OccurrenceCount);
}