using FinanceManager.Application.MoneyFlow.CashFlowForecast;
using FinanceManager.Domain.Currencies.Entities;
using FinanceManager.Domain.Currencies.Services;
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
    private readonly InMemoryOwnerLedger _ledger = new();
    private readonly Mock<ICurrencyExchangeService> _currencyExchange = new();
    private readonly Mock<IRecurringTransactionDetectorService> _recurring = new();
    private readonly FakeDateTimeProvider _clock = new(new DateTime(2026, 9, 16));
    private readonly CashFlowForecastService _service;

    // A primary constructor cannot be used because its body initializes _service from the fixture mocks.
    public CashFlowForecastServiceTests()
    {
        _service = new CashFlowForecastService(_ledger, _recurring.Object, _currencyExchange.Object, _clock);
    }

    [Fact]
    public async Task GetForecast_ProjectsRecurringIncomeExpensesAndSubscriptions()
    {
        AddCash(new DateTime(2026, 9, 10), 1000m);
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
    }

    [Fact]
    public async Task GetForecast_ExcludesMutedAndCancelledPatterns()
    {
        AddCash(new DateTime(2026, 9, 10), 1000m);
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
        AddCash(new DateTime(2026, 9, 10), 1000m);
        SetupFlows();

        var result = await _service.GetForecast(7, DefaultCurrency.PLN, CashFlowForecastHorizons.SixtyDays, TestContext.Current.CancellationToken);

        Assert.False(result.HasForecastableActivity);
        Assert.Empty(result.ExpectedTransactions);
        Assert.Equal(61, result.ForecastSeries.Count);
        Assert.All(result.ForecastSeries, point => Assert.Equal(1000m, point.Value));
    }

    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(0, 0, 1)]
    [InlineData(12, 0, 0)]
    [InlineData(23, 59, 59)]
    public async Task GetForecast_IncludesEntriesThroughCurrentUtcInstant(int hour, int minute, int second)
    {
        var now = new DateTimeOffset(2026, 9, 16, hour, minute, second, TimeSpan.Zero).UtcDateTime;
        _clock.SetUtcNow(now);
        // Balances are the running sum of the changes: 900, 1000, then 634 at the instant itself; the entry a tick later is not known yet.
        AddCash(now.Date.AddDays(-1).AddTicks(-1), 900m);
        AddCash(now.Date.AddTicks(-1), 100m);
        AddCash(now, -366m);
        AddCash(now.AddTicks(1), 9365m);
        SetupFlows();

        var result = await _service.GetForecast(7, DefaultCurrency.PLN, 30, TestContext.Current.CancellationToken);

        Assert.Equal(now.Date, result.AsOfDate);
        Assert.Equal(1000m, HistoricalValueOn(result, now.Date.AddDays(-1)));
        Assert.Equal(634m, result.HistoricalSeries[^1].Value);
        Assert.Equal(634m, result.ForecastSeries[0].Value);
    }

    [Fact]
    public async Task GetForecast_RefreshAfterBackdatedEntry_UpdatesCurrentBalanceOnNoEntryDay()
    {
        _clock.SetUtcNow(new DateTime(2026, 9, 16, 12, 0, 0, DateTimeKind.Utc));
        AddCash(_clock.UtcNow.AddDays(-2), 1000m);
        SetupFlows();
        var before = await _service.GetForecast(7, DefaultCurrency.PLN, 30, TestContext.Current.CancellationToken);

        AddCash(_clock.UtcNow.AddDays(-1), -366m);
        var after = await _service.GetForecast(7, DefaultCurrency.PLN, 30, TestContext.Current.CancellationToken);

        Assert.Equal(1000m, before.ForecastSeries[0].Value);
        Assert.Equal(634m, after.ForecastSeries[0].Value);
        Assert.Equal(after.HistoricalSeries[^1].Value, after.ForecastSeries[0].Value);
    }

    [Fact]
    public async Task GetForecast_RejectsUnsupportedHorizon()
    {
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            _service.GetForecast(7, DefaultCurrency.PLN, 45, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GetForecast_PoolsEveryOwnedCashEntry()
    {
        // Entries are pooled per owner, so cash booked on what used to be a loan account counts and an unlinked entry does too.
        _ledger.Add(DefaultCurrency.PLN.Id, new DateTime(2026, 9, 10), 1000m, [], "cash", null, 1)
            .Add(DefaultCurrency.PLN.Id, new DateTime(2026, 9, 10), 500m, [], "loan", null, 2)
            .Add(DefaultCurrency.PLN.Id, new DateTime(2026, 9, 11), -200m);
        SetupFlows();

        var result = await _service.GetForecast(7, DefaultCurrency.PLN, CashFlowForecastHorizons.ThirtyDays, TestContext.Current.CancellationToken);

        Assert.Equal(1500m, HistoricalValueOn(result, new DateTime(2026, 9, 10)));
        Assert.Equal(1300m, HistoricalValueOn(result, new DateTime(2026, 9, 16)));
    }

    [Fact]
    public async Task GetForecast_WithoutAnyEntries_ReturnsFlatZeroHistory_ForGuestOwner()
    {
        _recurring.Setup(x => x.GetRecurringCashFlows(-3, It.IsAny<DateTime>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);

        var result = await _service.GetForecast(-3, DefaultCurrency.PLN, CashFlowForecastHorizons.ThirtyDays, TestContext.Current.CancellationToken);

        Assert.Equal(31, result.HistoricalSeries.Count);
        Assert.All(result.HistoricalSeries, point => Assert.Equal(0m, point.Value));
    }

    [Fact]
    public async Task GetForecast_ValuesBalancesAndRecurringFlowsInRequestedCurrency()
    {
        AddCash(new DateTime(2026, 9, 10), 1000m);
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

    [Fact]
    public async Task GetForecast_WhenRequestedCurrencyRatesAreUnavailable_FailsExplicitly()
    {
        AddCash(new DateTime(2026, 9, 10), 1000m);
        SetupFlows();
        _currencyExchange
            .Setup(x => x.GetExchangeRateAsync(
                DefaultCurrency.PLN,
                DefaultCurrency.USD,
                new DateTime(2026, 8, 17),
                new DateTime(2026, 9, 16),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.GetForecast(
                7,
                DefaultCurrency.USD,
                CashFlowForecastHorizons.ThirtyDays,
                TestContext.Current.CancellationToken));

        Assert.Equal("No exchange rates are available from PLN to USD for the forecast period.", exception.Message);
        _recurring.Verify(
            x => x.GetRecurringCashFlows(
                It.IsAny<int>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GetForecast_PassesCapturedAsOfDateToRecurringDetector()
    {
        AddCash(new DateTime(2026, 9, 10), 1000m);
        SetupFlows();

        await _service.GetForecast(
            7,
            DefaultCurrency.PLN,
            CashFlowForecastHorizons.ThirtyDays,
            TestContext.Current.CancellationToken);

        _recurring.Verify(
            x => x.GetRecurringCashFlows(7, _clock.UtcNow.Date, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private void AddCash(DateTime date, decimal valueChange) => _ledger.Add(DefaultCurrency.PLN.Id, date, valueChange);

    private void SetupFlows(params RecurringCashFlow[] flows) =>
        _recurring
            .Setup(x => x.GetRecurringCashFlows(
                7,
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(flows.ToList());

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