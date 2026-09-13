using FinanceManager.Domain.Alerts.Enums;

namespace FinanceManager.Domain.Alerts.Commands;

public record UpdateFinancialAlert(
    string Title,
    bool IsEnabled,
    AlertComparisonOperator ComparisonOperator,
    decimal Threshold,
    AlertEvaluationPeriod EvaluationPeriod = AlertEvaluationPeriod.CurrentMonth,
    int? AccountId = null,
    int? LabelId = null,
    string? LabelName = null,
    string? MerchantName = null,
    Guid? SubscriptionId = null,
    TimeSpan? CooldownPeriod = null,
    AlertType? AlertType = null);