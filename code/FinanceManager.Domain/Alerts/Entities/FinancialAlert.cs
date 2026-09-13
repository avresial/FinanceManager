using FinanceManager.Domain.Alerts.Commands;
using FinanceManager.Domain.Alerts.Enums;

namespace FinanceManager.Domain.Alerts.Entities;

public class FinancialAlert
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public int UserId { get; set; }
    public string Title { get; set; } = string.Empty;
    public AlertType AlertType { get; set; }
    public bool IsEnabled { get; set; } = true;
    public AlertComparisonOperator ComparisonOperator { get; set; } = AlertComparisonOperator.GreaterThan;
    public decimal Threshold { get; set; }
    public AlertEvaluationPeriod EvaluationPeriod { get; set; } = AlertEvaluationPeriod.CurrentMonth;

    public int? AccountId { get; set; }
    public int? LabelId { get; set; }
    public string? LabelName { get; set; }
    public string? MerchantName { get; set; }
    public Guid? SubscriptionId { get; set; }

    public AlertTriggerStatus LastStatus { get; set; } = AlertTriggerStatus.Healthy;
    public DateTime? LastTriggeredAt { get; set; }
    public decimal? LastTriggeredValue { get; set; }
    public string? LastTriggeredConditionFingerprint { get; set; }
    public TimeSpan? CooldownPeriod { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    // Parameterless constructor for persistence and serialization frameworks.
    public FinancialAlert()
    {
    }

    public FinancialAlert(
        int userId,
        string title,
        AlertType alertType,
        AlertComparisonOperator comparisonOperator,
        decimal threshold,
        AlertEvaluationPeriod evaluationPeriod = AlertEvaluationPeriod.CurrentMonth,
        int? accountId = null,
        int? labelId = null,
        string? labelName = null,
        string? merchantName = null,
        Guid? subscriptionId = null,
        TimeSpan? cooldownPeriod = null)
    {
        UserId = userId;
        Title = title;
        AlertType = alertType;
        ComparisonOperator = comparisonOperator;
        Threshold = threshold;
        EvaluationPeriod = evaluationPeriod;
        AccountId = accountId;
        LabelId = labelId;
        LabelName = labelName;
        MerchantName = merchantName;
        SubscriptionId = subscriptionId;
        CooldownPeriod = cooldownPeriod;
    }

    public void UpdateFrom(UpdateFinancialAlert command)
    {
        Title = command.Title;
        IsEnabled = command.IsEnabled;
        ComparisonOperator = command.ComparisonOperator;
        Threshold = command.Threshold;
        EvaluationPeriod = command.EvaluationPeriod;
        AccountId = command.AccountId;
        LabelId = command.LabelId;
        LabelName = command.LabelName;
        MerchantName = command.MerchantName;
        SubscriptionId = command.SubscriptionId;
        CooldownPeriod = command.CooldownPeriod;
        if (command.AlertType is { } alertType)
            AlertType = alertType;
        UpdatedAt = DateTime.UtcNow;
    }

    public void RecordTrigger(decimal observedValue, string conditionFingerprint, DateTime triggeredAt)
    {
        LastStatus = AlertTriggerStatus.Triggered;
        LastTriggeredValue = observedValue;
        LastTriggeredConditionFingerprint = conditionFingerprint;
        LastTriggeredAt = triggeredAt;
        UpdatedAt = triggeredAt;
    }

    public void RecordResolved(DateTime evaluatedAt)
    {
        LastStatus = AlertTriggerStatus.Healthy;
        LastTriggeredConditionFingerprint = null;
        UpdatedAt = evaluatedAt;
    }

    public bool IsSuppressedByCooldown(DateTime currentTime)
    {
        if (CooldownPeriod is TimeSpan cooldown && LastTriggeredAt is DateTime lastTriggered)
        {
            return (currentTime - lastTriggered) < cooldown;
        }

        return false;
    }

    public bool IsUnchangedTrigger(string conditionFingerprint)
    {
        return LastStatus == AlertTriggerStatus.Triggered
            && string.Equals(LastTriggeredConditionFingerprint, conditionFingerprint, StringComparison.Ordinal);
    }
}