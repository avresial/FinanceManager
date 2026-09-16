using FinanceManager.Application.MoneyFlow.CashFlowForecast;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Shared.Entities;
using FinanceManager.Domain.FinancialAccounts.Shared.Repositories;
using FinanceManager.Domain.Identity.Entities;
using FinanceManager.Domain.Identity.Services;
using FinanceManager.Domain.Labels.Entities;
using FinanceManager.Domain.Labels.Services;
using FinanceManager.Domain.MoneyFlow.Entities;
using FinanceManager.Tests.Unit.Shared.Time;
using Moq;

namespace FinanceManager.Tests.Unit.Application.Services;

[Collection("Application")]
[Trait("Category", "Unit")]
public class CashFlowForecastServiceTests
{
    private readonly Mock<IFinancialAccountRepository> _accounts = new();
    private readonly Mock<IRecurringTransactionDetectorService> _recurring = new();
    private readonly FakeDateTimeProvider _clock = new(new DateTime(2026, 9, 16));
    private readonly CashFlowForecastService _service;

    public CashFlowForecastServiceTests()
    {
        _service = new CashFlowForecastService(_accounts.Object, _recurring.Object, _clock);
    }

    [Fact]
    public async Task GetForecast_ProjectsRecurringIncomeExpensesAndSubscriptions()
    {
        var account = Account(Entry(1, new DateTime(2026, 9, 10), 1000m));
        SetupAccounts(account);
        SetupFlows(
            Flow("Salary", 500m, new DateTime(2026, 9, 20), RecurringCadence.Monthly),
            Flow("Rent", -200m, new DateTime(2026, 9, 18), RecurringCadence.Monthly),
            Flow("Streaming subscription", -25m, new DateTime(2026, 9, 17), RecurringCadence.Monthly));

        var result = await _service.GetForecast(7, DefaultCurrency.PLN, CashFlowForecastHorizons.ThirtyDays, TestContext.Current.CancellationToken);

        Assert.True(result.HasForecastableActivity);
        Assert.Equal(31, result.ForecastSeries.Count);
        Assert.Equal(3, result.ExpectedTransactions.Count);
        Assert.Equal(975m, ValueOn(result, new DateTime(2026, 9, 17)));
        Assert.Equal(775m, ValueOn(result, new DateTime(2026, 9, 18)));
        Assert.Equal(1275m, ValueOn(result, new DateTime(2026, 9, 20)));
        Assert.Single(account.Entries);
    }

    [Fact]
    public async Task GetForecast_ExcludesMutedAndCancelledPatterns()
    {
        SetupAccounts(Account(Entry(1, new DateTime(2026, 9, 10), 1000m)));
        SetupFlows(
            Flow("Active", -100m, new DateTime(2026, 9, 20), RecurringCadence.Monthly),
            Flow("Muted", -50m, new DateTime(2026, 9, 20), RecurringCadence.Monthly, isMuted: true),
            Flow("Cancelled", -25m, new DateTime(2026, 9, 20), RecurringCadence.Monthly, isCancelled: true));

        var result = await _service.GetForecast(7, DefaultCurrency.PLN, CashFlowForecastHorizons.ThirtyDays, TestContext.Current.CancellationToken);

        var transaction = Assert.Single(result.ExpectedTransactions);
        Assert.Equal("Active", transaction.Description);
        Assert.Equal(-100m, transaction.Amount);
        Assert.Equal(900m, ValueOn(result, new DateTime(2026, 9, 20)));
    }

    [Fact]
    public async Task GetForecast_WithNoForecastableActivity_ReturnsExplicitStateAndFlatProjection()
    {
        SetupAccounts(Account(Entry(1, new DateTime(2026, 9, 10), 1000m)));
        SetupFlows();

        var result = await _service.GetForecast(7, DefaultCurrency.PLN, CashFlowForecastHorizons.SixtyDays, TestContext.Current.CancellationToken);

        Assert.False(result.HasForecastableActivity);
        Assert.Empty(result.ExpectedTransactions);
        Assert.Equal(61, result.ForecastSeries.Count);
        Assert.All(result.ForecastSeries, point => Assert.Equal(1000m, point.Value));
    }

    [Fact]
    public async Task GetForecast_RejectsUnsupportedHorizon()
    {
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            _service.GetForecast(7, DefaultCurrency.PLN, 45, TestContext.Current.CancellationToken));
    }

    private void SetupAccounts(CurrencyAccount account) =>
        _accounts
            .Setup(x => x.GetAccounts<CurrencyAccount>(
                7,
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<bool>()))
            .Returns(new[] { account }.ToAsyncEnumerable());

    private void SetupFlows(params RecurringCashFlow[] flows) =>
        _recurring
            .Setup(x => x.GetRecurringCashFlows(7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(flows.ToList());

    private static CurrencyAccount Account(params CurrencyAccountEntry[] entries) =>
        new(7, 1, "Cash", entries, AccountLabel.Cash);

    private static CurrencyAccountEntry Entry(int id, DateTime date, decimal value) =>
        new(1, id, date, value, value);

    private static RecurringCashFlow Flow(
        string name,
        decimal amount,
        DateTime nextExpectedDate,
        RecurringCadence cadence,
        bool isMuted = false,
        bool isCancelled = false) =>
        new()
        {
            Name = name,
            MonthlyAmount = amount,
            OccurrenceAmount = amount,
            Cadence = cadence,
            NextExpectedDate = nextExpectedDate,
            IsMuted = isMuted,
            IsCancelled = isCancelled
        };

    private static decimal ValueOn(CashFlowForecast forecast, DateTime date) =>
        forecast.ForecastSeries.Single(point => point.DateTime.Date == date.Date).Value;
}