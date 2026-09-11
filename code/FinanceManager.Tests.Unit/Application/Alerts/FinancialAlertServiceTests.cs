using FinanceManager.Application.Alerts.Models;
using FinanceManager.Application.Alerts.Services;
using FinanceManager.Domain.Alerts.Commands;
using FinanceManager.Domain.Alerts.Entities;
using FinanceManager.Domain.Alerts.Enums;
using FinanceManager.Domain.Alerts.Repositories;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Shared.Entities;
using FinanceManager.Domain.FinancialAccounts.Shared.Repositories;
using FinanceManager.Domain.Labels.Entities;
using FinanceManager.Domain.Labels.Services;
using FinanceManager.Domain.Shared.Services;
using Microsoft.Extensions.Logging;
using Moq;

namespace FinanceManager.Tests.Unit.Application.Alerts;

[Trait("Category", "Unit")]
public class FinancialAlertServiceTests
{
    private readonly Mock<IFinancialAlertRepository> _alertRepositoryMock = new();
    private readonly Mock<IFinancialAccountRepository> _accountRepositoryMock = new();
    private readonly Mock<IRecurringTransactionDetectorService> _recurringServiceMock = new();
    private readonly Mock<IFinancialAlertEvaluator> _evaluatorMock = new();
    private readonly Mock<IDateTimeProvider> _dateTimeProviderMock = new();
    private readonly Mock<ILogger<FinancialAlertService>> _loggerMock = new();

    private readonly FinancialAlertService _service;
    private readonly DateTime _now = new(2026, 9, 11, 15, 0, 0, DateTimeKind.Utc);

    public FinancialAlertServiceTests()
    {
        _dateTimeProviderMock.Setup(d => d.UtcNow).Returns(_now);
        _service = new FinancialAlertService(
            _alertRepositoryMock.Object,
            _accountRepositoryMock.Object,
            _recurringServiceMock.Object,
            _evaluatorMock.Object,
            _dateTimeProviderMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task GetAlertsAsync_ReturnsAlertsFromRepository()
    {
        var alerts = new List<FinancialAlert>
        {
            new(1, "Alert 1", AlertType.AccountBalance, AlertComparisonOperator.LessThan, 1000m)
        };
        _alertRepositoryMock.Setup(r => r.GetAlertsByUserId(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(alerts);

        var result = await _service.GetAlertsAsync(1, TestContext.Current.CancellationToken);

        Assert.Single(result);
        Assert.Equal("Alert 1", result[0].Title);
    }

    [Fact]
    public async Task CreateAlertAsync_AddsAlertToRepository()
    {
        var command = new CreateFinancialAlert(
            Title: "New Alert",
            AlertType: AlertType.CategorySpending,
            ComparisonOperator: AlertComparisonOperator.GreaterThan,
            Threshold: 500m);

        _alertRepositoryMock.Setup(r => r.Add(It.IsAny<FinancialAlert>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((FinancialAlert a, CancellationToken _) => a);

        var result = await _service.CreateAlertAsync(1, command, TestContext.Current.CancellationToken);

        Assert.Equal("New Alert", result.Title);
        Assert.Equal(500m, result.Threshold);
        _alertRepositoryMock.Verify(r => r.Add(It.IsAny<FinancialAlert>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateAlertAsync_UpdatesExistingAlertInRepository()
    {
        var alertId = Guid.NewGuid();
        var existing = new FinancialAlert(1, "Old Title", AlertType.AccountBalance, AlertComparisonOperator.LessThan, 1000m)
        {
            Id = alertId
        };

        _alertRepositoryMock.Setup(r => r.GetById(1, alertId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var command = new UpdateFinancialAlert(
            Title: "Updated Title",
            IsEnabled: true,
            ComparisonOperator: AlertComparisonOperator.LessThanOrEqual,
            Threshold: 1200m);

        var result = await _service.UpdateAlertAsync(1, alertId, command, TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Equal("Updated Title", result.Title);
        Assert.Equal(1200m, result.Threshold);
        _alertRepositoryMock.Verify(r => r.Update(existing, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SetEnabledAsync_TogglesEnabledAndPersists()
    {
        var alertId = Guid.NewGuid();
        var existing = new FinancialAlert(1, "Alert", AlertType.AccountBalance, AlertComparisonOperator.LessThan, 1000m)
        {
            Id = alertId,
            IsEnabled = true
        };

        _alertRepositoryMock.Setup(r => r.GetById(1, alertId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var result = await _service.SetEnabledAsync(1, alertId, false, TestContext.Current.CancellationToken);

        Assert.True(result);
        Assert.False(existing.IsEnabled);
        _alertRepositoryMock.Verify(r => r.Update(existing, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteAlertAsync_CallsRepositoryDelete()
    {
        var alertId = Guid.NewGuid();
        _alertRepositoryMock.Setup(r => r.Delete(1, alertId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await _service.DeleteAlertAsync(1, alertId, TestContext.Current.CancellationToken);

        Assert.True(result);
        _alertRepositoryMock.Verify(r => r.Delete(1, alertId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task EvaluateAlertsAsync_CoordinatesEvaluationAndPersistsTriggeredState()
    {
        var alert = new FinancialAlert(1, "Low Cash", AlertType.AccountBalance, AlertComparisonOperator.LessThan, 1000m);
        _alertRepositoryMock.Setup(r => r.GetAlertsByUserId(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync([alert]);

        var account = new CurrencyAccount(1, 1, "Cash", AccountLabel.Cash);
        _accountRepositoryMock.Setup(r => r.GetAccounts<CurrencyAccount>(1, It.IsAny<DateTime>(), It.IsAny<DateTime>(), true))
            .Returns(new[] { account }.ToAsyncEnumerable());

        _recurringServiceMock.Setup(r => r.GetRecurringTransactions(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var outcome = new AlertEvaluationOutcome(
            alert.Id,
            alert.Title,
            alert.AlertType,
            AlertTriggerStatus.Triggered,
            IsTriggered: true,
            IsNewlyTriggered: true,
            IsSuppressed: false,
            DeDuplicationReason.None,
            CurrentValue: 500m,
            Threshold: 1000m,
            ComparisonOperator: AlertComparisonOperator.LessThan,
            ConditionFingerprint: "fp-1",
            Message: "Triggered",
            EvaluatedAt: _now,
            Context: new Dictionary<string, string>());

        _evaluatorMock.Setup(e => e.EvaluateAll(It.IsAny<IEnumerable<FinancialAlert>>(), It.IsAny<AlertEvaluationSnapshot>()))
            .Returns([outcome]);

        var results = await _service.EvaluateAlertsAsync(1, TestContext.Current.CancellationToken);

        Assert.Single(results);
        Assert.True(results[0].IsNewlyTriggered);
        Assert.Equal(AlertTriggerStatus.Triggered, alert.LastStatus);
        Assert.Equal(500m, alert.LastTriggeredValue);
        Assert.Equal("fp-1", alert.LastTriggeredConditionFingerprint);

        _alertRepositoryMock.Verify(r => r.Update(alert, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task EvaluateAlertsAsync_FailureIsolation_CatchesExceptionAndReturnsEmptyWithoutCrashing()
    {
        _alertRepositoryMock.Setup(r => r.GetAlertsByUserId(1, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("DB connection error"));

        var results = await _service.EvaluateAlertsAsync(1, TestContext.Current.CancellationToken);

        Assert.Empty(results);
    }

    [Fact]
    public async Task EvaluateAlertsAsync_AllTimeAlertLoadsCompleteAccountHistory()
    {
        var alert = new FinancialAlert(
            1,
            "All history",
            AlertType.LargeTransaction,
            AlertComparisonOperator.GreaterThan,
            1000m,
            evaluationPeriod: AlertEvaluationPeriod.AllTime);
        _alertRepositoryMock.Setup(r => r.GetAlertsByUserId(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync([alert]);
        _accountRepositoryMock
            .Setup(r => r.GetAccounts<CurrencyAccount>(
                1,
                DateTime.MinValue,
                It.IsAny<DateTime>(),
                true))
            .Returns(new[] { new CurrencyAccount(1, 1, "Cash", AccountLabel.Cash) }.ToAsyncEnumerable());
        _recurringServiceMock.Setup(r => r.GetRecurringTransactions(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _evaluatorMock.Setup(e => e.EvaluateAll(It.IsAny<IEnumerable<FinancialAlert>>(), It.IsAny<AlertEvaluationSnapshot>()))
            .Returns([new AlertEvaluationOutcome(
                alert.Id,
                alert.Title,
                alert.AlertType,
                AlertTriggerStatus.Healthy,
                IsTriggered: false,
                IsNewlyTriggered: false,
                IsSuppressed: false,
                DeDuplicationReason.None,
                CurrentValue: 0m,
                Threshold: alert.Threshold,
                ComparisonOperator: alert.ComparisonOperator,
                ConditionFingerprint: "none",
                Message: "Healthy",
                EvaluatedAt: _now,
                Context: new Dictionary<string, string>())]);

        var results = await _service.EvaluateAlertsAsync(1, TestContext.Current.CancellationToken);

        Assert.Single(results);
        _accountRepositoryMock.Verify(r => r.GetAccounts<CurrencyAccount>(
            1,
            DateTime.MinValue,
            It.IsAny<DateTime>(),
            true), Times.Once);
    }
}