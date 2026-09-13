using FinanceManager.Domain.Alerts.Commands;
using FinanceManager.Domain.Alerts.Dtos;
using FinanceManager.Domain.Alerts.Entities;
using FinanceManager.Domain.Alerts.Enums;

namespace FinanceManager.Tests.Unit.Domain.Alerts;

[Trait("Category", "Unit")]
public class FinancialAlertTests
{
    [Fact]
    public void Constructor_InitializesDefaultValues()
    {
        var alert = new FinancialAlert();

        Assert.NotEqual(Guid.Empty, alert.Id);
        Assert.True(alert.IsEnabled);
        Assert.Equal(AlertComparisonOperator.GreaterThan, alert.ComparisonOperator);
        Assert.Equal(AlertEvaluationPeriod.CurrentMonth, alert.EvaluationPeriod);
        Assert.Equal(AlertTriggerStatus.Healthy, alert.LastStatus);
        Assert.Null(alert.LastTriggeredAt);
        Assert.Null(alert.LastTriggeredValue);
        Assert.Null(alert.LastTriggeredConditionFingerprint);
        Assert.Null(alert.CooldownPeriod);
    }

    [Fact]
    public void ParameterizedConstructor_SetsAllConfiguredProperties()
    {
        var id = Guid.NewGuid();
        var alert = new FinancialAlert(
            userId: 42,
            title: "Dining Alert",
            alertType: AlertType.CategorySpending,
            comparisonOperator: AlertComparisonOperator.GreaterThanOrEqual,
            threshold: 500m,
            evaluationPeriod: AlertEvaluationPeriod.CurrentMonth,
            accountId: 2,
            labelId: 10,
            labelName: "Dining",
            merchantName: null,
            subscriptionId: null,
            cooldownPeriod: TimeSpan.FromHours(12));

        Assert.Equal(42, alert.UserId);
        Assert.Equal("Dining Alert", alert.Title);
        Assert.Equal(AlertType.CategorySpending, alert.AlertType);
        Assert.Equal(AlertComparisonOperator.GreaterThanOrEqual, alert.ComparisonOperator);
        Assert.Equal(500m, alert.Threshold);
        Assert.Equal(AlertEvaluationPeriod.CurrentMonth, alert.EvaluationPeriod);
        Assert.Equal(2, alert.AccountId);
        Assert.Equal(10, alert.LabelId);
        Assert.Equal("Dining", alert.LabelName);
        Assert.Equal(TimeSpan.FromHours(12), alert.CooldownPeriod);
        Assert.True(alert.IsEnabled);
    }

    [Fact]
    public void UpdateFrom_UpdatesPropertiesAndSetsUpdatedAt()
    {
        var alert = new FinancialAlert(
            userId: 1,
            title: "Old Title",
            alertType: AlertType.AccountBalance,
            comparisonOperator: AlertComparisonOperator.LessThan,
            threshold: 1000m);

        var updateCommand = new UpdateFinancialAlert(
            Title: "New Title",
            IsEnabled: false,
            ComparisonOperator: AlertComparisonOperator.LessThanOrEqual,
            Threshold: 1500m,
            EvaluationPeriod: AlertEvaluationPeriod.Last30Days,
            AccountId: 5,
            LabelId: null,
            LabelName: null,
            MerchantName: "Amazon",
            SubscriptionId: null,
            CooldownPeriod: TimeSpan.FromDays(1),
            AlertType: AlertType.MerchantSpending);

        alert.UpdateFrom(updateCommand);

        Assert.Equal("New Title", alert.Title);
        Assert.False(alert.IsEnabled);
        Assert.Equal(AlertComparisonOperator.LessThanOrEqual, alert.ComparisonOperator);
        Assert.Equal(1500m, alert.Threshold);
        Assert.Equal(AlertType.MerchantSpending, alert.AlertType);
        Assert.Equal(AlertEvaluationPeriod.Last30Days, alert.EvaluationPeriod);
        Assert.Equal(5, alert.AccountId);
        Assert.Equal("Amazon", alert.MerchantName);
        Assert.Equal(TimeSpan.FromDays(1), alert.CooldownPeriod);
        Assert.NotNull(alert.UpdatedAt);
    }

    [Fact]
    public void RecordTrigger_SetsTriggeredStateAndFingerprint()
    {
        var alert = new FinancialAlert(1, "Balance", AlertType.AccountBalance, AlertComparisonOperator.LessThan, 1000m);
        var triggeredTime = new DateTime(2026, 9, 11, 12, 0, 0, DateTimeKind.Utc);
        var fingerprint = "AccountBalance:AccountId=1:Balance=500.00";

        alert.RecordTrigger(500m, fingerprint, triggeredTime);

        Assert.Equal(AlertTriggerStatus.Triggered, alert.LastStatus);
        Assert.Equal(500m, alert.LastTriggeredValue);
        Assert.Equal(fingerprint, alert.LastTriggeredConditionFingerprint);
        Assert.Equal(triggeredTime, alert.LastTriggeredAt);
        Assert.Equal(triggeredTime, alert.UpdatedAt);
    }

    [Fact]
    public void RecordResolved_SetsHealthyStateAndClearsFingerprint()
    {
        var alert = new FinancialAlert(1, "Balance", AlertType.AccountBalance, AlertComparisonOperator.LessThan, 1000m);
        alert.RecordTrigger(500m, "fp", DateTime.UtcNow);

        var resolvedTime = DateTime.UtcNow.AddMinutes(5);
        alert.RecordResolved(resolvedTime);

        Assert.Equal(AlertTriggerStatus.Healthy, alert.LastStatus);
        Assert.Null(alert.LastTriggeredConditionFingerprint);
        Assert.Equal(resolvedTime, alert.UpdatedAt);
    }

    [Fact]
    public void IsUnchangedTrigger_IdentifiesIdenticalFingerprintWhenTriggered()
    {
        var alert = new FinancialAlert(1, "Balance", AlertType.AccountBalance, AlertComparisonOperator.LessThan, 1000m);
        alert.RecordTrigger(500m, "exact-fingerprint", DateTime.UtcNow);

        Assert.True(alert.IsUnchangedTrigger("exact-fingerprint"));
        Assert.False(alert.IsUnchangedTrigger("different-fingerprint"));

        alert.RecordResolved(DateTime.UtcNow);
        Assert.False(alert.IsUnchangedTrigger("exact-fingerprint"));
    }

    [Fact]
    public void IsSuppressedByCooldown_ReturnsTrueWithinWindowAndFalseOutside()
    {
        var alert = new FinancialAlert(1, "Balance", AlertType.AccountBalance, AlertComparisonOperator.LessThan, 1000m)
        {
            CooldownPeriod = TimeSpan.FromHours(1)
        };

        var triggerTime = new DateTime(2026, 9, 11, 10, 0, 0, DateTimeKind.Utc);
        alert.RecordTrigger(500m, "fp", triggerTime);

        Assert.True(alert.IsSuppressedByCooldown(triggerTime.AddMinutes(30)));
        Assert.False(alert.IsSuppressedByCooldown(triggerTime.AddMinutes(61)));
    }

    [Fact]
    public void FinancialAlertDto_FromEntity_MapsAllFieldsCorrectly()
    {
        var alert = new FinancialAlert(
            userId: 7,
            title: "Test Alert",
            alertType: AlertType.LargeTransaction,
            comparisonOperator: AlertComparisonOperator.GreaterThan,
            threshold: 2000m,
            evaluationPeriod: AlertEvaluationPeriod.Last7Days,
            accountId: 3,
            cooldownPeriod: TimeSpan.FromHours(2));

        var dto = FinancialAlertDto.FromEntity(alert);

        Assert.Equal(alert.Id, dto.Id);
        Assert.Equal(7, dto.UserId);
        Assert.Equal("Test Alert", dto.Title);
        Assert.Equal(AlertType.LargeTransaction, dto.AlertType);
        Assert.Equal(AlertComparisonOperator.GreaterThan, dto.ComparisonOperator);
        Assert.Equal(2000m, dto.Threshold);
        Assert.Equal(AlertEvaluationPeriod.Last7Days, dto.EvaluationPeriod);
        Assert.Equal(3, dto.AccountId);
        Assert.Equal(TimeSpan.FromHours(2), dto.CooldownPeriod);
    }
}