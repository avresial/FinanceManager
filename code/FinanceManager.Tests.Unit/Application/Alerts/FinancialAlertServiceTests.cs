using FinanceManager.Application.Alerts.Models;
using FinanceManager.Application.Alerts.Services;
using FinanceManager.Domain.Alerts.Commands;
using FinanceManager.Domain.Alerts.Entities;
using FinanceManager.Domain.Alerts.Enums;
using FinanceManager.Domain.Alerts.Models;
using FinanceManager.Domain.Alerts.Repositories;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Currencies.Repositories;
using FinanceManager.Domain.FinancialAccounts.Shared.Entities;
using FinanceManager.Domain.Labels.Services;
using FinanceManager.Domain.Shared.Services;
using FinanceManager.Domain.Transactions.Cash.Dtos;
using FinanceManager.Domain.Transactions.Cash.Repositories;
using FinanceManager.Domain.Transactions.Shared.Dtos;
using Microsoft.Extensions.Logging;
using Moq;

namespace FinanceManager.Tests.Unit.Application.Alerts;

[Trait("Category", "Unit")]
public class FinancialAlertServiceTests
{
    private readonly Mock<IFinancialAlertRepository> _alertRepositoryMock = new();
    private readonly Mock<ICashTransactionReader> _cashReaderMock = new();
    private readonly Mock<ICurrencyRepository> _currencyRepositoryMock = new();
    private readonly Mock<IRecurringTransactionDetectorService> _recurringServiceMock = new();
    private readonly Mock<IFinancialAlertEvaluator> _evaluatorMock = new();
    private readonly Mock<IDateTimeProvider> _dateTimeProviderMock = new();
    private readonly Mock<ILogger<FinancialAlertService>> _loggerMock = new();

    private readonly FinancialAlertService _service;
    private readonly DateTime _now = new(2026, 9, 11, 15, 0, 0, DateTimeKind.Utc);

    public FinancialAlertServiceTests()
    {
        _dateTimeProviderMock.Setup(d => d.UtcNow).Returns(_now);
        _currencyRepositoryMock.Setup(r => r.GetCurrencies(It.IsAny<CancellationToken>()))
            .Returns(new[] { DefaultCurrency.PLN, new Currency(1, "EUR", "€") }.ToAsyncEnumerable());
        _currencyRepositoryMock.Setup(r => r.GetCurrency(0, It.IsAny<CancellationToken>())).ReturnsAsync(DefaultCurrency.PLN);
        _currencyRepositoryMock.Setup(r => r.GetCurrency(1, It.IsAny<CancellationToken>())).ReturnsAsync(new Currency(1, "EUR", "€"));
        _currencyRepositoryMock.Setup(r => r.GetCurrency(99, It.IsAny<CancellationToken>())).ReturnsAsync((Currency?)null);
        _recurringServiceMock.Setup(r => r.GetRecurringTransactions(It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
        _service = new FinancialAlertService(
            _alertRepositoryMock.Object,
            _cashReaderMock.Object,
            _currencyRepositoryMock.Object,
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
            AlertType: AlertType.AccountBalance,
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

        Assert.Equal(SetAlertEnabledStatus.Updated, result);
        Assert.False(existing.IsEnabled);
        _alertRepositoryMock.Verify(r => r.Update(existing, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SetEnabledAsync_UnknownAlert_ReturnsNotFound()
    {
        var result = await _service.SetEnabledAsync(-3, Guid.NewGuid(), true, TestContext.Current.CancellationToken);

        Assert.Equal(SetAlertEnabledStatus.NotFound, result);
    }

    [Theory]
    [InlineData(AlertType.AccountBalance)]
    [InlineData(AlertType.CategorySpending)]
    [InlineData(AlertType.MerchantSpending)]
    [InlineData(AlertType.LargeTransaction)]
    public async Task SetEnabledAsync_MigratedCashAlertWithoutCurrency_CannotBeEnabled(AlertType type)
    {
        var existing = new FinancialAlert(-3, "Migrated", type, AlertComparisonOperator.LessThan, 1000m) { IsEnabled = false };
        _alertRepositoryMock.Setup(r => r.GetById(-3, existing.Id, It.IsAny<CancellationToken>())).ReturnsAsync(existing);

        var result = await _service.SetEnabledAsync(-3, existing.Id, true, TestContext.Current.CancellationToken);

        Assert.Equal(SetAlertEnabledStatus.CurrencyRequired, result);
        Assert.False(existing.IsEnabled);
        _alertRepositoryMock.Verify(r => r.Update(It.IsAny<FinancialAlert>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SetEnabledAsync_CashAlertWithPlnCurrency_CanBeEnabled()
    {
        var existing = new FinancialAlert(-3, "Migrated", AlertType.AccountBalance, AlertComparisonOperator.LessThan, 1000m, currencyId: 0) { IsEnabled = false };
        _alertRepositoryMock.Setup(r => r.GetById(-3, existing.Id, It.IsAny<CancellationToken>())).ReturnsAsync(existing);

        var result = await _service.SetEnabledAsync(-3, existing.Id, true, TestContext.Current.CancellationToken);

        Assert.Equal(SetAlertEnabledStatus.Updated, result);
        Assert.True(existing.IsEnabled);
    }

    [Theory]
    [InlineData(AlertType.AccountBalance, null, "Select a currency for this alert.")]
    [InlineData(AlertType.LargeTransaction, 99, "The selected currency does not exist.")]
    [InlineData(AlertType.SubscriptionPriceChange, 1, "Subscription alerts do not use a currency.")]
    [InlineData(AlertType.AccountBalance, 0, null)]
    [InlineData(AlertType.CategorySpending, 1, null)]
    [InlineData(AlertType.SubscriptionPriceChange, null, null)]
    public async Task ValidateCurrencyAsync_AppliesOneRuleForEveryAlertType(AlertType type, int? currencyId, string? expectedError)
    {
        var error = await _service.ValidateCurrencyAsync(type, currencyId, TestContext.Current.CancellationToken);

        Assert.Equal(expectedError, error);
    }

    [Fact]
    public async Task CreateAlertAsync_StoresCurrencyAndNeverAnAccount()
    {
        var command = new CreateFinancialAlert("Low cash", AlertType.AccountBalance, AlertComparisonOperator.LessThan, 100m, CurrencyId: 1);
        _alertRepositoryMock.Setup(r => r.Add(It.IsAny<FinancialAlert>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((FinancialAlert a, CancellationToken _) => a);

        var result = await _service.CreateAlertAsync(-3, command, TestContext.Current.CancellationToken);

        Assert.Equal(1, result.CurrencyId);
        Assert.Null(result.AccountId);
        Assert.Equal(-3, result.UserId);
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
        var alert = new FinancialAlert(1, "Low Cash", AlertType.AccountBalance, AlertComparisonOperator.LessThan, 1000m, currencyId: 0);
        _alertRepositoryMock.Setup(r => r.GetAlertsByUserId(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync([alert]);
        _cashReaderMock.Setup(r => r.GetPositions(1, _now, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new OwnerLedgerPosition(0, 500m, _now.AddDays(-1))]);

        AlertEvaluationSnapshot? snapshot = null;
        var outcome = new AlertEvaluationOutcome(
            alert.Id,
            alert.Title,
            alert.AlertType,
            AlertTriggerStatus.Triggered,
            IsTriggered: true,
            TriggeredAt: _now,
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
            .Callback<IEnumerable<FinancialAlert>, AlertEvaluationSnapshot>((_, s) => snapshot = s)
            .Returns([outcome]);

        var results = await _service.EvaluateAlertsAsync(1, TestContext.Current.CancellationToken);

        Assert.Single(results);
        Assert.Equal(_now, results[0].TriggeredAt);
        Assert.Equal(AlertTriggerStatus.Triggered, alert.LastStatus);
        Assert.Equal(500m, alert.LastTriggeredValue);
        Assert.Equal("fp-1", alert.LastTriggeredConditionFingerprint);
        Assert.Equal(500m, Assert.Single(snapshot!.CashPositions).Value);
        Assert.Equal("PLN", snapshot.CurrencyCodes[0]);

        _alertRepositoryMock.Verify(r => r.Update(alert, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task EvaluateAlertsAsync_OnlyLoadsWhatEnabledAlertsNeed()
    {
        var subscription = new FinancialAlert(-5, "Subscription", AlertType.SubscriptionPriceChange, AlertComparisonOperator.GreaterThan, 0m);
        var disabledCash = new FinancialAlert(-5, "Migrated", AlertType.LargeTransaction, AlertComparisonOperator.GreaterThan, 10m) { IsEnabled = false };
        _alertRepositoryMock.Setup(r => r.GetAlertsByUserId(-5, It.IsAny<CancellationToken>()))
            .ReturnsAsync([subscription, disabledCash]);
        _evaluatorMock.Setup(e => e.EvaluateAll(It.IsAny<IEnumerable<FinancialAlert>>(), It.IsAny<AlertEvaluationSnapshot>())).Returns([]);

        await _service.EvaluateAlertsAsync(-5, TestContext.Current.CancellationToken);

        _recurringServiceMock.Verify(r => r.GetRecurringTransactions(-5, It.IsAny<CancellationToken>()), Times.Once);
        _cashReaderMock.Verify(r => r.GetPositions(It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
        _cashReaderMock.Verify(r => r.GetHistory(It.IsAny<int>(), It.IsAny<OwnerHistoryQuery>(), It.IsAny<CancellationToken>()), Times.Never);
        _currencyRepositoryMock.Verify(r => r.GetCurrencies(It.IsAny<CancellationToken>()), Times.Never);
        _alertRepositoryMock.Verify(r => r.GetAllTimeEvaluationData(It.IsAny<int>(), It.IsAny<IReadOnlyCollection<FinancialAlert>>(),
            It.IsAny<DateTime>(), It.IsAny<CancellationToken>(), It.IsAny<bool>()), Times.Never);
    }

    [Fact]
    public async Task EvaluateAlertsAsync_BoundedAlertsReadTheOwnerHistoryOfTheLongestPeriodInKeysetPages()
    {
        var month = new FinancialAlert(-5, "Month", AlertType.LargeTransaction, AlertComparisonOperator.GreaterThan, 10m, currencyId: 0);
        var week = new FinancialAlert(-5, "Week", AlertType.MerchantSpending, AlertComparisonOperator.GreaterThan, 10m,
            evaluationPeriod: AlertEvaluationPeriod.Last7Days, currencyId: 0);
        _alertRepositoryMock.Setup(r => r.GetAlertsByUserId(-5, It.IsAny<CancellationToken>())).ReturnsAsync([month, week]);
        var firstPage = Enumerable.Range(1, 500).Select(i => Entry(i + 1000, _now.AddMinutes(-i))).ToList();
        var secondPage = new List<CashLogEntry> { Entry(7, _now.AddDays(-9)) };
        var queries = new List<OwnerHistoryQuery>();
        _cashReaderMock.Setup(r => r.GetHistory(-5, It.IsAny<OwnerHistoryQuery>(), It.IsAny<CancellationToken>()))
            .Callback<int, OwnerHistoryQuery, CancellationToken>((_, q, _) => queries.Add(q))
            .ReturnsAsync((int _, OwnerHistoryQuery q, CancellationToken _) => q.Boundary is null ? firstPage : secondPage);
        AlertEvaluationSnapshot? snapshot = null;
        _evaluatorMock.Setup(e => e.EvaluateAll(It.IsAny<IEnumerable<FinancialAlert>>(), It.IsAny<AlertEvaluationSnapshot>()))
            .Callback<IEnumerable<FinancialAlert>, AlertEvaluationSnapshot>((_, s) => snapshot = s)
            .Returns([]);

        await _service.EvaluateAlertsAsync(-5, TestContext.Current.CancellationToken);

        Assert.Equal(2, queries.Count);
        Assert.All(queries, q => Assert.Equal(new DateTime(2026, 9, 1), q.Start));
        Assert.All(queries, q => Assert.Equal(_now, q.End));
        Assert.Equal(new OwnerHistoryBoundary(firstPage[^1].PostingDate, firstPage[^1].EntryId, false), queries[1].Boundary);
        Assert.Equal(501, snapshot!.RecentEntries.Count);
        _cashReaderMock.Verify(r => r.GetPositions(It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
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
    public async Task EvaluateAlertsAsync_AllTimeAlertUsesAggregatedDataWithoutReadingHistory()
    {
        var alert = new FinancialAlert(
            1,
            "All history",
            AlertType.LargeTransaction,
            AlertComparisonOperator.GreaterThan,
            1000m,
            evaluationPeriod: AlertEvaluationPeriod.AllTime,
            currencyId: 0);
        _alertRepositoryMock.Setup(r => r.GetAlertsByUserId(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync([alert]);
        _alertRepositoryMock
            .Setup(r => r.GetAllTimeEvaluationData(
                1,
                It.IsAny<IReadOnlyCollection<FinancialAlert>>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>(),
                false))
            .ReturnsAsync((IReadOnlyDictionary<Guid, FinancialAlertEvaluationData>)new Dictionary<Guid, FinancialAlertEvaluationData>());
        _evaluatorMock.Setup(e => e.EvaluateAll(It.IsAny<IEnumerable<FinancialAlert>>(), It.IsAny<AlertEvaluationSnapshot>()))
            .Returns([new AlertEvaluationOutcome(
                alert.Id,
                alert.Title,
                alert.AlertType,
                AlertTriggerStatus.Healthy,
                IsTriggered: false,
                TriggeredAt: null,
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
        _cashReaderMock.Verify(r => r.GetHistory(It.IsAny<int>(), It.IsAny<OwnerHistoryQuery>(), It.IsAny<CancellationToken>()), Times.Never);
        _alertRepositoryMock.Verify(r => r.GetAllTimeEvaluationData(
            1,
            It.Is<IReadOnlyCollection<FinancialAlert>>(items => items.Count == 1 && items.Single().Id == alert.Id),
            _now,
            It.IsAny<CancellationToken>(),
            false), Times.Once);
    }

    [Fact]
    public async Task EvaluateAlertAsync_DetailedRequestIncludesAllMatchingTransactions()
    {
        var alert = new FinancialAlert(
            1,
            "All history",
            AlertType.CategorySpending,
            AlertComparisonOperator.GreaterThan,
            1000m,
            evaluationPeriod: AlertEvaluationPeriod.AllTime,
            currencyId: 0);
        _alertRepositoryMock.Setup(r => r.GetById(1, alert.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(alert);
        _alertRepositoryMock
            .Setup(r => r.GetAllTimeEvaluationData(
                1,
                It.IsAny<IReadOnlyCollection<FinancialAlert>>(),
                _now,
                It.IsAny<CancellationToken>(),
                true))
            .ReturnsAsync((IReadOnlyDictionary<Guid, FinancialAlertEvaluationData>)new Dictionary<Guid, FinancialAlertEvaluationData>());
        var outcome = new AlertEvaluationOutcome(
            alert.Id,
            alert.Title,
            alert.AlertType,
            AlertTriggerStatus.Triggered,
            IsTriggered: true,
            TriggeredAt: _now,
            IsSuppressed: false,
            DeDuplicationReason.None,
            CurrentValue: 1250m,
            Threshold: alert.Threshold,
            ComparisonOperator: alert.ComparisonOperator,
            ConditionFingerprint: "detailed",
            Message: "Triggered",
            EvaluatedAt: _now,
            Context: new Dictionary<string, string>(),
            MatchingTransactionCount: 2,
            OccurrenceCount: 2);
        _evaluatorMock.Setup(e => e.Evaluate(
                alert,
                It.Is<AlertEvaluationSnapshot>(snapshot => snapshot.IncludeAllMatchingTransactions)))
            .Returns(outcome);

        var result = await _service.EvaluateAlertAsync(
            1,
            alert.Id,
            TestContext.Current.CancellationToken,
            includeAllMatchingTransactions: true);

        Assert.Equal(outcome, result);
        _alertRepositoryMock.Verify(r => r.GetAllTimeEvaluationData(
            1,
            It.Is<IReadOnlyCollection<FinancialAlert>>(items => items.Count == 1 && items.Single().Id == alert.Id),
            _now,
            It.IsAny<CancellationToken>(),
            true), Times.Once);
    }

    [Fact]
    public async Task EvaluateAlertsAsync_ErrorOutcomeDoesNotResolveTriggeredAlert()
    {
        var alert = new FinancialAlert(1, "Unstable alert", AlertType.AccountBalance, AlertComparisonOperator.LessThan, 1000m, currencyId: 0);
        alert.RecordTrigger(500m, "previous", _now.AddMinutes(-1));
        _alertRepositoryMock.Setup(r => r.GetAlertsByUserId(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync([alert]);

        var errorOutcome = new AlertEvaluationOutcome(
            alert.Id,
            alert.Title,
            alert.AlertType,
            AlertTriggerStatus.Error,
            IsTriggered: false,
            TriggeredAt: null,
            IsSuppressed: false,
            DeDuplicationReason.None,
            CurrentValue: 0m,
            Threshold: alert.Threshold,
            ComparisonOperator: alert.ComparisonOperator,
            ConditionFingerprint: string.Empty,
            Message: "Evaluation failed",
            EvaluatedAt: _now,
            Context: new Dictionary<string, string>(),
            ErrorMessage: "provider unavailable");
        _evaluatorMock.Setup(e => e.EvaluateAll(It.IsAny<IEnumerable<FinancialAlert>>(), It.IsAny<AlertEvaluationSnapshot>()))
            .Returns([errorOutcome]);

        var results = await _service.EvaluateAlertsAsync(1, TestContext.Current.CancellationToken);

        Assert.Equal(AlertTriggerStatus.Error, results.Single().Status);
        Assert.Equal(AlertTriggerStatus.Triggered, alert.LastStatus);
        Assert.Equal("previous", alert.LastTriggeredConditionFingerprint);
        _alertRepositoryMock.Verify(r => r.Update(It.IsAny<FinancialAlert>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static CashLogEntry Entry(long id, DateTime postingDate) =>
        new(null, id, postingDate, -50m, "Shop", 0, "PLN", null, Array.Empty<FinancialLabel>());
}