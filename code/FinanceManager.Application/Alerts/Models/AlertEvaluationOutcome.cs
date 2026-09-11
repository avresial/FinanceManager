using FinanceManager.Domain.Alerts.Enums;

namespace FinanceManager.Application.Alerts.Models;

public record AlertEvaluationOutcome(
    Guid AlertId,
    string AlertTitle,
    AlertType AlertType,
    AlertTriggerStatus Status,
    bool IsTriggered,
    bool IsNewlyTriggered,
    bool IsSuppressed,
    DeDuplicationReason DeDuplicationReason,
    decimal CurrentValue,
    decimal Threshold,
    AlertComparisonOperator ComparisonOperator,
    string ConditionFingerprint,
    string Message,
    DateTime EvaluatedAt,
    IReadOnlyDictionary<string, string> Context,
    string? ErrorMessage = null);