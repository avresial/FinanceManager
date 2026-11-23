using FinanceManager.Application.FinancialAccounts.Bond.Valuation;
using FinanceManager.Application.MoneyFlow.NetWorth;
using FinanceManager.Domain.FinancialAccounts.Bond.Entities;
using FinanceManager.Domain.FinancialAccounts.Bond.Repositories;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Investments.Services;
using FinanceManager.Domain.Shared;
using Moq;

namespace FinanceManager.Tests.Unit.Application.Services;

[Collection("Application")]
[Trait("Category", "Unit")]
public class NetWorthServiceTests
{
    private readonly InMemoryOwnerLedger _cash = new();
    private readonly InMemoryOwnerLedger _bonds = new();
    private readonly Mock<IBondDetailsRepository> _bondDetailsRepositoryMock = new();
    private readonly Mock<IInvestmentValuationService> _investmentValuationServiceMock = new();

    public NetWorthServiceTests()
    {
        SetBondDetails();
        _investmentValuationServiceMock
            .Setup(x => x.GetUserValueByListingAsync(It.IsAny<int>(), It.IsAny<Currency>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<long, decimal>());
        _investmentValuationServiceMock
            .Setup(x => x.GetUserValueSeriesAsync(It.IsAny<int>(), It.IsAny<Currency>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<DateTime, decimal>());
    }

    [Fact]
    public async Task GetNetWorth_NoEntries_ReturnsZero()
    {
        Assert.Equal(0, await NewService().GetNetWorth(1, DefaultCurrency.PLN, DateTime.UtcNow));
    }

    [Fact]
    public async Task GetNetWorth_PoolsCashAcrossEntriesAndCurrencies_WithoutConvertingFx()
    {
        var date = DateTime.UtcNow;
        _cash.Add(0, date.AddDays(-3), 100m).Add(0, date.AddDays(-1), 40m).Add(2, date.AddDays(-1), -30m).Add(0, date.AddDays(1), 999m);

        Assert.Equal(110m, await NewService().GetNetWorth(1, DefaultCurrency.PLN, date));
    }

    [Fact]
    public async Task GetNetWorth_AcceptsNegativeGuestIds()
    {
        _cash.Add(0, DateTime.UtcNow.AddDays(-1), 100m);

        Assert.Equal(100m, await NewService().GetNetWorth(-12, DefaultCurrency.PLN, DateTime.UtcNow));
    }

    [Fact]
    public async Task GetNetWorth_AddsThePooledInvestmentValue()
    {
        var date = DateTime.UtcNow;
        _investmentValuationServiceMock
            .Setup(x => x.GetUserValueByListingAsync(1, It.IsAny<Currency>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<long, decimal> { [5] = 200m, [6] = 50m });
        _cash.Add(0, date.AddDays(-1), 10m);

        Assert.Equal(260m, await NewService().GetNetWorth(1, DefaultCurrency.PLN, date));
    }

    [Fact]
    public async Task GetNetWorth_Point_ValuesPooledBondUnitsAtTheDefinitionsUnitValue()
    {
        var date = new DateTime(2024, 3, 1, 12, 0, 0, DateTimeKind.Utc);
        _bonds.Add(1, date.AddDays(-9), 3m).Add(1, date.AddDays(-2), 2m).Add(2, date.AddDays(-1), 4m);
        SetBondDetails(CreateBond(1, 100m), CreateBond(2, 10m));

        Assert.Equal(540m, await NewService().GetNetWorth(1, DefaultCurrency.PLN, date));
    }

    [Fact]
    public async Task GetNetWorthSeries_UsesTheDailyInvestmentSeriesOnceForTheOwner()
    {
        var end = DateTime.UtcNow.Date;
        var start = end.AddDays(-2);
        _investmentValuationServiceMock
            .Setup(x => x.GetUserValueSeriesAsync(1, It.IsAny<Currency>(), start, end, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<DateTime, decimal> { [start] = 10m, [start.AddDays(1)] = 20m, [end] = 30m });

        var result = await NewService().GetNetWorth(1, DefaultCurrency.PLN, start, end);

        Assert.Equal(10m, result[start]);
        Assert.Equal(20m, result[start.AddDays(1)]);
        Assert.Equal(30m, result[end]);
        _investmentValuationServiceMock.Verify(
            x => x.GetUserValueSeriesAsync(It.IsAny<int>(), It.IsAny<Currency>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetNetWorth_OverRange_KeysEachPointByCalendarDay()
    {
        // The dashboard passes "now" as the end, so the walk back carries the time of day.
        // Keying by date keeps the series on the same day grid as the other dashboard series.
        var start = new DateTime(2024, 3, 3, 0, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(2024, 3, 6, 20, 23, 14, DateTimeKind.Utc);

        var result = await NewService().GetNetWorth(1, DefaultCurrency.PLN, start, end);

        Assert.Equal([start, start.AddDays(1), start.AddDays(2), start.AddDays(3)], result.Keys.Order());
    }

    [Fact]
    public async Task GetNetWorth_OverRange_CarriesPooledCashAndBondsAcrossDays()
    {
        var start = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var end = start.AddDays(2);
        _cash.Add(0, start.AddDays(-5), 100m).Add(2, start.AddDays(1), -10m);
        _bonds.Add(1, start.AddDays(-30), 2m).Add(1, start.AddDays(2), 3m);
        SetBondDetails(CreateBond(1, 100m));

        var result = await NewService().GetNetWorth(1, DefaultCurrency.PLN, start, end);

        Assert.Equal(300m, result[start]);
        Assert.Equal(290m, result[start.AddDays(1)]);
        Assert.Equal(590m, result[end]);
    }

    [Fact]
    public async Task GetNetWorth_OverRange_RestartsBondAccrualFromTheLatestPooledEntry()
    {
        var start = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var end = start.AddDays(40);
        var details = CreateBond(1, 100m, 0.05m);
        _bonds.Add(1, start.AddDays(-100), 1m).Add(1, start.AddDays(20), 1m);
        SetBondDetails(details);

        var result = await NewService().GetNetWorth(1, DefaultCurrency.PLN, start, end);

        // Before the second purchase the first unit accrues from its own entry; afterwards all two units accrue from the latest one.
        var beforePurchase = BondAccountEntry.GetPrice(DateOnly.FromDateTime(start.AddDays(-100)), 1m, DateOnly.FromDateTime(start.AddDays(19)), details)[DateOnly.FromDateTime(start.AddDays(19))];
        var afterPurchase = BondAccountEntry.GetPrice(DateOnly.FromDateTime(start.AddDays(20)), 2m, DateOnly.FromDateTime(end), details)[DateOnly.FromDateTime(end)];
        Assert.Equal(Math.Round(beforePurchase, 2), result[start.AddDays(19)]);
        Assert.Equal(Math.Round(afterPurchase, 2), result[end]);
        Assert.Equal(Math.Round(2m * 100m, 2) * 1, Math.Round(BondAccountEntry.GetPrice(DateOnly.FromDateTime(start.AddDays(20)), 2m, DateOnly.FromDateTime(start.AddDays(20)), details)[DateOnly.FromDateTime(start.AddDays(20))], 2));
    }

    [Fact]
    public async Task GetNetWorth_OverRange_LongHeldBond_PreparesHistoryOnce()
    {
        var end = new DateTime(2024, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        var start = end.AddDays(-30);
        var details = CreateBond(1, 100m, 0.05m);
        _bonds.Add(1, start.AddDays(-3650), 1m);
        SetBondDetails(details);
        var anchor = DateOnly.FromDateTime(start.AddDays(-3650));

        // Warm JIT outside measurement; each operation has a new request context.
        await NewService().GetNetWorth(1, DefaultCurrency.PLN, start, end);
        var beforeHistory = GC.GetAllocatedBytesForCurrentThread();
        var history = BondAccountEntry.GetPrice(anchor, 1m, DateOnly.FromDateTime(end), details);
        var historyBytes = GC.GetAllocatedBytesForCurrentThread() - beforeHistory;
        var service = NewService();
        var beforeRange = GC.GetAllocatedBytesForCurrentThread();
        var pending = service.GetNetWorth(1, DefaultCurrency.PLN, start, end);
        // All mock I/O completes synchronously, so the per-thread allocation comparison is valid.
        Assert.True(pending.IsCompletedSuccessfully);
        var result = await pending;
        var rangeBytes = GC.GetAllocatedBytesForCurrentThread() - beforeRange;

        Assert.Equal(31, result.Count);
        Assert.All(result, point => Assert.Equal(Math.Round(history[DateOnly.FromDateTime(point.Key)], 2), point.Value));
        Assert.True(rangeBytes < historyBytes * 4 + 100_000,
            $"Range allocated {rangeBytes} bytes versus {historyBytes} for one bounded history.");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    [InlineData(12)]
    public async Task GetNetWorth_OverRange_MatchesPointPricing_WithChangingAndSameDayEntries(int hour)
    {
        var start = new DateTime(2023, 12, 28, hour, 13, 0, DateTimeKind.Utc);
        var end = start.AddDays(12);
        var details = CreateBond(1, 1.23456m);
        details.CalculationMethods =
        [
            new() { BondDetails = details, DateOperator = DateOperator.UntilDate, DateValue = "2024-01-02", Rate = 0.037m },
            // An inactive method leaves a genuine gap after the last active method expires.
            new() { BondDetails = null, DateOperator = DateOperator.UntilDate, DateValue = "2030-01-01", Rate = 1m },
            new() { BondDetails = details, DateOperator = DateOperator.UntilDate, DateValue = "2024-01-05", Rate = 0.061m }
        ];
        SetBondDetails(details, CreateBond(2, 100m, 0.02m));
        _bonds.Add(1, start.Date.AddDays(-365), 2.12m)
            .Add(1, start.AddDays(4).Date.AddHours(18), 3.25m)
            .Add(1, start.AddDays(4).Date.AddHours(18), 99m)
            .Add(1, start.AddDays(4).Date.AddHours(8), 5m)
            .Add(1, start.AddDays(8), -3.25m)
            .Add(1, start.AddDays(9), 2.75m)
            .Add(2, start.AddDays(1), 1m);
        _cash.Add(0, start.AddDays(3), 10.005m);

        var range = await NewService().GetNetWorth(1, DefaultCurrency.PLN, start, end);

        Assert.Equal(13, range.Count);
        for (var date = end; date >= start; date = date.AddDays(-1))
            Assert.Equal(await NewService().GetNetWorth(1, DefaultCurrency.PLN, date), range[date.Date]);
        Assert.Empty(await NewService().GetNetWorth(1, DefaultCurrency.PLN, end, start));
    }

    [Fact]
    public async Task GetNetWorth_ValuesAnEntryAtTheWalkBackInstant_SoAPastDaysLaterEntryJoinsTheNextDay()
    {
        var start = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(2024, 1, 3, 10, 0, 0, DateTimeKind.Utc);
        _cash.Add(0, new DateTime(2024, 1, 2, 20, 0, 0, DateTimeKind.Utc), 50m);

        var result = await NewService().GetNetWorth(1, DefaultCurrency.PLN, start, end);

        Assert.Equal(0m, result[new DateTime(2024, 1, 2)]);
        Assert.Equal(50m, result[new DateTime(2024, 1, 3)]);
    }

    [Fact]
    public async Task GetNetWorth_ZeroUnitPosition_DoesNotNeedADefinition_ButAnActiveOneDoes()
    {
        var date = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        _bonds.Add(7, date.AddDays(-2), 3m).Add(7, date.AddDays(-1), -3m);
        SetBondDetails();
        Assert.Equal(0m, await NewService().GetNetWorth(1, DefaultCurrency.PLN, date));

        _bonds.Add(1, date, 1m);
        var missing = await Assert.ThrowsAsync<InvalidOperationException>(() => NewService().GetNetWorth(1, DefaultCurrency.PLN, date, date));
        Assert.Equal("Bond valuation requires details for bond id 1.", missing.Message);

        var details = CreateBond(1, 100m);
        details.CalculationMethods.Clear();
        SetBondDetails(details);
        var methods = await Assert.ThrowsAsync<InvalidOperationException>(() => NewService().GetNetWorth(1, DefaultCurrency.PLN, date, date));
        Assert.Equal("CalculationMethods can't be empty", methods.Message);
    }

    private void SetBondDetails(params BondDetails[] details) =>
        _bondDetailsRepositoryMock.Setup(x => x.GetByIdsAsync(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<CancellationToken>())).ReturnsAsync(details);

    private NetWorthService NewService() => new(
        _cash,
        _bonds,
        new BondDashboardContext(_bondDetailsRepositoryMock.Object),
        _investmentValuationServiceMock.Object);

    private static BondDetails CreateBond(int id, decimal unitValue, decimal rate = 0m) =>
        new(
            $"Bond {id}",
            "Issuer",
            new DateOnly(2023, 1, 1),
            new DateOnly(2030, 1, 1),
            [new BondCalculationMethod { DateOperator = DateOperator.UntilDate, DateValue = "2030-01-01", Rate = rate }],
            DefaultCurrency.PLN,
            BondType.InflationBond,
            unitValue)
        {
            Id = id
        };
}