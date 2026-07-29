using FinanceManager.Components.Features.FinancialAccounts.Models;
using FinanceManager.Components.Features.FinancialAccounts.Services;
using FinanceManager.Components.Shared.Models;
using FinanceManager.Components.Shared.Services;
using FinanceManager.Domain.MoneyFlow.Entities;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace FinanceManager.Tests.Unit.Components.Features.FinancialAccounts.Services;

[Trait("Category", "Unit")]
public class AccountChartSnapshotStoreTests
{
    private const string _currencyKey = "account-chart:currency:1:2:3:Month";
    private const string _bondKey = "account-chart:bond:1:2:3:Month";
    private const string _investmentKey = "account-chart:investment:1:2:3:42:3M";
    private static readonly DateTime _start = new(2026, 1, 1);
    private static readonly DateTime _end = new(2026, 2, 1);
    private readonly Mock<ISnapshotService> _snapshots = new();

    private AccountChartSnapshotStore CreateStore() =>
        new(new SnapshotRefreshCoordinator(_snapshots.Object, NullLogger<SnapshotRefreshCoordinator>.Instance));

    [Fact]
    public async Task Currency_Unchanged_PaintsBeforeFetch_AlwaysFetches_AndSkipsWrite()
    {
        _snapshots.Setup(x => x.GetAsync<AccountChartSnapshot>(_currencyKey))
            .ReturnsAsync(StandardSnapshot("currency", StandardModel(10m)));
        var painted = false;
        var fetchedAfterPaint = false;

        var result = await CreateStore().RefreshCurrencyAsync(
            1,
            2,
            3,
            "Month",
            new RefreshVersionGate(),
            null,
            () =>
            {
                fetchedAfterPaint = painted;
                return Task.FromResult<AccountChartModel?>(StandardModel(10m));
            },
            model =>
            {
                painted = model.Series.Single().Value == 10m;
                return Task.CompletedTask;
            });

        Assert.True(fetchedAfterPaint);
        Assert.Equal(SnapshotRefreshOutcome.Unchanged, result.Outcome);
        _snapshots.Verify(x => x.SetAsync(It.IsAny<string>(), It.IsAny<AccountChartSnapshot>()), Times.Never);
    }

    [Fact]
    public async Task Bond_Changed_WritesItsOwnScopedSnapshot()
    {
        var result = await CreateStore().RefreshBondAsync(
            1,
            2,
            3,
            "Month",
            new RefreshVersionGate(),
            null,
            () => Task.FromResult<AccountChartModel?>(StandardModel(20m)));

        Assert.Equal(SnapshotRefreshOutcome.Refreshed, result.Outcome);
        _snapshots.Verify(x => x.SetAsync(_bondKey, It.Is<AccountChartSnapshot>(
            snapshot => snapshot.Variant == "bond"
                && snapshot.UserId == 1
                && snapshot.AccountId == 2
                && snapshot.CurrencyId == 3
                && snapshot.Model.Series.Single().Value == 20m)), Times.Once);
        _snapshots.Verify(x => x.GetAsync<AccountChartSnapshot>(_currencyKey), Times.Never);
    }

    [Fact]
    public async Task Currency_RejectsMismatchedSnapshotContext()
    {
        _snapshots.Setup(x => x.GetAsync<AccountChartSnapshot>(_currencyKey))
            .ReturnsAsync(new AccountChartSnapshot
            {
                Variant = "bond",
                UserId = 99,
                AccountId = 2,
                CurrencyId = 3,
                RangeKey = "Month",
                Model = StandardModel(10m)
            });
        var painted = false;

        var result = await CreateStore().RefreshCurrencyAsync(
            1,
            2,
            3,
            "Month",
            new RefreshVersionGate(),
            null,
            () => Task.FromResult<AccountChartModel?>(StandardModel(10m)),
            _ =>
            {
                painted = true;
                return Task.CompletedTask;
            });

        Assert.False(painted);
        Assert.False(result.SnapshotPainted);
        Assert.Equal(SnapshotRefreshOutcome.Refreshed, result.Outcome);
    }

    [Fact]
    public async Task Currency_SnapshotForAnotherRange_IsNeitherReadNorPainted()
    {
        // A "Month" snapshot must not paint into a "1Y" view: the series covers the wrong window,
        // and painting it would fight the range the user just selected.
        _snapshots.Setup(x => x.GetAsync<AccountChartSnapshot>(_currencyKey))
            .ReturnsAsync(StandardSnapshot("currency", StandardModel(10m)));
        var painted = false;

        var result = await CreateStore().RefreshCurrencyAsync(
            1,
            2,
            3,
            "1Y",
            new RefreshVersionGate(),
            null,
            () => Task.FromResult<AccountChartModel?>(StandardModel(10m)),
            _ =>
            {
                painted = true;
                return Task.CompletedTask;
            });

        Assert.False(painted);
        Assert.False(result.SnapshotPainted);
        Assert.Equal(SnapshotRefreshOutcome.Refreshed, result.Outcome);
        _snapshots.Verify(x => x.GetAsync<AccountChartSnapshot>(_currencyKey), Times.Never);
        _snapshots.Verify(x => x.SetAsync("account-chart:currency:1:2:3:1Y", It.IsAny<AccountChartSnapshot>()), Times.Once);
    }

    [Fact]
    public void BuildRangeKey_CustomRange_CarriesItsDates()
    {
        Assert.Equal("Month", AccountChartSnapshotStore.BuildRangeKey("Month", _start, _end));
        Assert.Equal("Custom:20260101:20260201", AccountChartSnapshotStore.BuildRangeKey("Custom", _start, _end));
    }

    [Fact]
    public async Task Currency_FailedRefresh_KeepsPaintedChart()
    {
        _snapshots.Setup(x => x.GetAsync<AccountChartSnapshot>(_currencyKey))
            .ReturnsAsync(StandardSnapshot("currency", StandardModel(10m)));

        var result = await CreateStore().RefreshCurrencyAsync(
            1,
            2,
            3,
            "Month",
            new RefreshVersionGate(),
            null,
            () => throw new HttpRequestException());

        Assert.Equal(SnapshotRefreshOutcome.Failed, result.Outcome);
        Assert.True(result.SnapshotPainted);
        Assert.False(result.IsBlockingFailure);
    }

    [Fact]
    public async Task Bond_NoSnapshotFailure_RemainsBlocking()
    {
        var result = await CreateStore().RefreshBondAsync(
            1,
            2,
            3,
            "Month",
            new RefreshVersionGate(),
            null,
            () => throw new HttpRequestException());

        Assert.Equal(SnapshotRefreshOutcome.Failed, result.Outcome);
        Assert.True(result.IsBlockingFailure);
    }

    [Fact]
    public async Task Investment_Changed_WritesCompleteRenderedModel()
    {
        var model = InvestmentModel(30m);

        var result = await CreateStore().RefreshInvestmentAsync(
            1,
            2,
            3,
            42,
            "3M",
            new RefreshVersionGate(),
            null,
            () => Task.FromResult<InvestmentAccountChartModel?>(model));

        Assert.Equal(SnapshotRefreshOutcome.Refreshed, result.Outcome);
        _snapshots.Verify(x => x.SetAsync(_investmentKey, It.Is<InvestmentAccountChartSnapshot>(
            snapshot => snapshot.UserId == 1
                && snapshot.AccountId == 2
                && snapshot.CurrencyId == 3
                && snapshot.BenchmarkListingId == 42
                && snapshot.Model.Series.Single().Value == 30m
                && snapshot.Model.BenchmarkSeries.Single().Value == 25m
                && snapshot.Model.BenchmarkName == "WIG20"
                && snapshot.Model.CurrentBalance == 30m
                && snapshot.Model.CapitalValue == 20m
                && snapshot.Model.CurrentValue == 30m
                && snapshot.Model.Holdings.Single().Ticker == "ABC")), Times.Once);
    }

    [Fact]
    public async Task Investment_SupersededRefresh_DoesNotWrite()
    {
        var gate = new RefreshVersionGate();

        var result = await CreateStore().RefreshInvestmentAsync(
            1,
            2,
            3,
            null,
            "3M",
            gate,
            null,
            () =>
            {
                gate.Claim();
                return Task.FromResult<InvestmentAccountChartModel?>(InvestmentModel(30m));
            });

        Assert.Equal(SnapshotRefreshOutcome.Superseded, result.Outcome);
        _snapshots.Verify(x => x.SetAsync(It.IsAny<string>(), It.IsAny<InvestmentAccountChartSnapshot>()), Times.Never);
    }

    private static AccountChartSnapshot StandardSnapshot(string variant, AccountChartModel model) =>
        new() { Variant = variant, UserId = 1, AccountId = 2, CurrencyId = 3, RangeKey = "Month", Model = model };

    private static AccountChartModel StandardModel(decimal value) =>
        new("Month", _start, _end, [new(_end, value)], value, 5m, 50m);

    private static InvestmentAccountChartModel InvestmentModel(decimal value) =>
        new(
            "3M",
            _start,
            _end,
            [new(_end, value)],
            [new(_end, 25m)],
            "WIG20",
            value,
            20m,
            value,
            value - 20m,
            50m,
            [new(1, "ABC", "Exchange", "USD", 2m, 15m, 30m)]);
}