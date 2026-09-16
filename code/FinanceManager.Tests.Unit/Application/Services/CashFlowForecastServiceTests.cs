using FinanceManager.Application.MoneyFlow.CashFlowForecast;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Currencies.Services;
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
    private readonly Mock<ICurrencyExchangeService> _currencyExchange = new();
    private readonly Mock<IRecurringTransactionDetectorService> _recurring = new();
    private readonly FakeDateTimeProvider _clock = new(new DateTime(2026, 9, 16));
    private readonly CashFlowForecastService _service;

    // A primary constructor cannot be used because its body initializes _service from the fixture mocks.
    public CashFlowForecastServiceTests()
    {
        _service = new CashFlowForecastService(_accounts.Object, _recurring.Object, _currencyExchange.Object, _clock);
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

    [Fact]
    public async Task GetForecast_IncludesOnlyCashAccounts()
    {
        SetupAccounts(
            Account(Entry(1, new DateTime(2026, 9, 10), 1000m)),
            new CurrencyAccount(
                7,
                2,
                "Loan",
                [new CurrencyAccountEntry(2, 1, new DateTime(2026, 9, 10), 500m, 500m)],
                AccountLabel.Loan));
        SetupFlows();

        var result = await _service.GetForecast(
            7,
            DefaultCurrency.PLN,
            CashFlowForecastHorizons.ThirtyDays,
            TestContext.Current.CancellationToken);

        Assert.Equal(1000m, HistoricalValueOn(result, new DateTime(2026, 9, 16)));
    }

    [Fact]
    public async Task GetForecast_ValuesBalancesAndRecurringFlowsInRequestedCurrency()
    {
        SetupAccounts(Account(Entry(1, new DateTime(2026, 9, 10), 1000m)));
        SetupFlows(Flow("Salary", 100m, new DateTime(2026, 9, 20), RecurringCadence.Monthly));
        _currencyExchange
            .Setup(x => x.GetExchangeRateAsync(
                DefaultCurrency.PLN,
                DefaultCurrency.USD,
                new DateTime(2026, 8, 17),
                new DateTime(2026, 9, 16),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Enumerable.Range(0, 31)
                .Select(offset => (new DateTime(2026, 8, 17).AddDays(offset), (decimal?)0.25m))
                .ToList());

        var result = await _service.GetForecast(
            7,
            DefaultCurrency.USD,
            CashFlowForecastHorizons.ThirtyDays,
            TestContext.Current.CancellationToken);

        Assert.Equal(250m, HistoricalValueOn(result, new DateTime(2026, 9, 16)));
        Assert.Equal(25m, Assert.Single(result.ExpectedTransactions).Amount);
        Assert.Equal(275m, ValueOn(result, new DateTime(2026, 9, 20)));
    }

    private void SetupAccounts(params CurrencyAccount[] accounts) =>
        _accounts
            .Setup(x => x.GetAccounts<CurrencyAccount>(
                7,
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<bool>()))
            .Returns(accounts.ToAsyncEnumerable());

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

    private static decimal HistoricalValueOn(CashFlowForecast forecast, DateTime date) =>
        forecast.HistoricalSeries.Single(point => point.DateTime.Date == date.Date).Value;
}