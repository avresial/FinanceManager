using FinanceManager.Domain.Alerts.Entities;
using FinanceManager.Domain.Alerts.Enums;

namespace FinanceManager.Domain.Alerts.Dtos;

public record FinancialAlertDto(
    Guid Id,
    int UserId,
    string Title,
    AlertType AlertType,
    bool IsEnabled,
    AlertComparisonOperator ComparisonOperator,
    decimal Threshold,
    AlertEvaluationPeriod EvaluationPeriod,
    int? AccountId,
    int? LabelId,
    string? LabelName,
    string? MerchantName,
    Guid? SubscriptionId,
    AlertTriggerStatus LastStatus,
    DateTime? LastTriggeredAt,
    decimal? LastTriggeredValue,
    TimeSpan? CooldownPeriod,
    DateTime CreatedAt,
    DateTime? UpdatedAt)
{
    public static FinancialAlertDto FromEntity(FinancialAlert entity) => new(
        entity.Id,
        entity.UserId,
        entity.Title,
        entity.AlertType,
        entity.IsEnabled,
        entity.ComparisonOperator,
        entity.Threshold,
        entity.EvaluationPeriod,
        entity.AccountId,
        entity.LabelId,
        entity.LabelName,
        entity.MerchantName,
        entity.SubscriptionId,
        entity.LastStatus,
        entity.LastTriggeredAt,
        entity.LastTriggeredValue,
        entity.CooldownPeriod,
        entity.CreatedAt,
        entity.UpdatedAt);
}