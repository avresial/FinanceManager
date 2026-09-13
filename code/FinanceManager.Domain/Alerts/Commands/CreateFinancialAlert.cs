using FinanceManager.Domain.Alerts.Enums;

namespace FinanceManager.Domain.Alerts.Commands;

public record CreateFinancialAlert(
    string Title,
    AlertType AlertType,
    AlertComparisonOperator ComparisonOperator,
    decimal Threshold,
    AlertEvaluationPeriod EvaluationPeriod = AlertEvaluationPeriod.CurrentMonth,
    int? AccountId = null,
    int? LabelId = null,
    string? LabelName = null,
    string? MerchantName = null,
    Guid? SubscriptionId = null,
    TimeSpan? CooldownPeriod = null);