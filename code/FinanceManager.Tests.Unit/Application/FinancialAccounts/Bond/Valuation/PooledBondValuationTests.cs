using FinanceManager.Application.FinancialAccounts.Bond.Valuation;
using FinanceManager.Domain.FinancialAccounts.Bond.Entities;
using FinanceManager.Domain.FinancialAccounts.Bond.Repositories;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.Shared;
using FinanceManager.Domain.Transactions.Shared;
using FinanceManager.Domain.Transactions.Shared.Dtos;
using Moq;

namespace FinanceManager.Tests.Unit.Application.FinancialAccounts.Bond.Valuation;

[Collection("Application")]
[Trait("Category", "Unit")]
public class PooledBondValuationTests
{
    private static readonly DateOnly _anchor = new(2024, 1, 1);

    private readonly BondDetails _details = CreateDetails(1, 100m, 0.05m);
    private readonly BondDashboardContext _context;

    public PooledBondValuationTests()
    {
        var repository = new Mock<IBondDetailsRepository>();
        repository.Setup(x => x.GetByIdsAsync(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyCollection<int> ids, CancellationToken _) => ids.Contains(1) ? [_details] : []);
        _context = new BondDashboardContext(repository.Object);
    }

    private static OwnerLedgerPosition Position(decimal units, DateOnly anchor, int bondId = 1) =>
        new(bondId, units, anchor.ToDateTime(new TimeOnly(9, 30), DateTimeKind.Utc));

    [Fact]
    public async Task ValueOn_AccruesPooledUnitsFromTheLatestEntry()
    {
        var position = Position(3m, _anchor);
        await PooledBondValuation.LoadDetailsAsync(_context, [position], TestContext.Current.CancellationToken);
        var date = _anchor.AddDays(40);

        var value = new PooledBondValuation(_context, date).ValueOn(position, date);

        Assert.Equal(BondAccountEntry.GetPrice(_anchor, 3m, date, _details)[date], value);
        Assert.True(value > 3m * 100m);
    }

    [Fact]
    public async Task ValueOn_IsTheUnitValueOnTheAnchorDay_AndZeroBeforeIt()
    {
        var position = Position(2m, _anchor);
        await PooledBondValuation.LoadDetailsAsync(_context, [position], TestContext.Current.CancellationToken);
        var valuation = new PooledBondValuation(_context, _anchor);

        Assert.Equal(200m, valuation.ValueOn(position, _anchor));
        Assert.Equal(0m, valuation.ValueOn(position, _anchor.AddDays(-1)));
    }

    [Fact]
    public async Task TryValueOn_ReturnsTheValueOfAnActivePosition_AndZeroForAnEmptyOne()
    {
        var position = Position(2m, _anchor);
        await PooledBondValuation.LoadDetailsAsync(_context, [position], TestContext.Current.CancellationToken);
        var valuation = new PooledBondValuation(_context, _anchor);

        Assert.True(valuation.TryValueOn(position, _anchor, out var value));
        Assert.Equal(200m, value);
        Assert.True(valuation.TryValueOn(Position(0m, _anchor), _anchor, out var empty));
        Assert.Equal(0m, empty);
    }

    [Fact]
    public void TryValueOn_IsFalseInsteadOfThrowingWhenTheBondDefinitionIsMissing()
    {
        var valuation = new PooledBondValuation(_context, _anchor);

        Assert.False(valuation.TryValueOn(Position(1m, _anchor, bondId: 99), _anchor, out var value));
        Assert.Equal(0m, value);
    }

    [Fact]
    public async Task ValueOn_RestartsAccrualWhenAnotherEntryMovesTheAnchor()
    {
        var late = _anchor.AddDays(200);
        var original = Position(1m, _anchor);
        var pooled = Position(2m, late);
        await PooledBondValuation.LoadDetailsAsync(_context, [original, pooled], TestContext.Current.CancellationToken);
        var valuation = new PooledBondValuation(_context, late.AddDays(10));
        var date = late.AddDays(10);

        var separately = valuation.ValueOn(original, date) + valuation.ValueOn(Position(1m, late), date);
        var together = valuation.ValueOn(pooled, date);

        // Pooled units accrue from the latest entry, so the first unit's earlier interest is not carried.
        Assert.Equal(BondAccountEntry.GetPrice(late, 2m, date, _details)[date], together);
        Assert.NotEqual(separately, together);
    }

    [Fact]
    public async Task ValueOn_ZeroUnitsNeverNeedsADefinition()
    {
        var valuation = new PooledBondValuation(_context, _anchor);

        Assert.Equal(0m, valuation.ValueOn(Position(0m, _anchor, bondId: 99), _anchor));
        await PooledBondValuation.LoadDetailsAsync(_context, [Position(0m, _anchor, bondId: 99)], TestContext.Current.CancellationToken);
        Assert.Throws<InvalidOperationException>(() => _context.GetDetails(99));
    }

    [Fact]
    public async Task ValueOn_ThrowsTheExistingErrorsForAnActivePositionWithoutDefinitionOrMethods()
    {
        var missing = Position(1m, _anchor, bondId: 99);
        await PooledBondValuation.LoadDetailsAsync(_context, [missing], TestContext.Current.CancellationToken);
        var error = Assert.Throws<InvalidOperationException>(() => new PooledBondValuation(_context, _anchor).ValueOn(missing, _anchor));
        Assert.Equal("Bond valuation requires details for bond id 99.", error.Message);

        _details.CalculationMethods.Clear();
        var noMethods = Position(1m, _anchor);
        await PooledBondValuation.LoadDetailsAsync(_context, [noMethods], TestContext.Current.CancellationToken);
        var methods = Assert.Throws<InvalidOperationException>(() => new PooledBondValuation(_context, _anchor).ValueOn(noMethods, _anchor));
        Assert.Equal("CalculationMethods can't be empty", methods.Message);
    }

    [Fact]
    public async Task ValueOn_LongHeldBond_PreparesOneHistoryForTheWholeRange()
    {
        var anchor = _anchor.AddDays(-3650);
        var position = Position(1m, anchor);
        await PooledBondValuation.LoadDetailsAsync(_context, [position], TestContext.Current.CancellationToken);
        var end = _anchor.AddDays(30);
        var valuation = new PooledBondValuation(_context, end);
        var expected = BondAccountEntry.GetPrice(anchor, 1m, end, _details);

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var date = _anchor; date <= end; date = date.AddDays(1))
            Assert.Equal(expected[date], valuation.ValueOn(position, date));
        var rangeBytes = GC.GetAllocatedBytesForCurrentThread() - before;

        var beforeHistory = GC.GetAllocatedBytesForCurrentThread();
        BondAccountEntry.GetPrice(anchor, 1m, end, _details);
        var historyBytes = GC.GetAllocatedBytesForCurrentThread() - beforeHistory;
        Assert.True(rangeBytes < historyBytes * 4 + 100_000, $"Range allocated {rangeBytes} bytes versus {historyBytes} for one history.");
    }

    [Fact]
    public async Task ValueOn_SharesMemoizedPricesWithTheRequestContext()
    {
        var position = Position(2m, _anchor);
        await PooledBondValuation.LoadDetailsAsync(_context, [position], TestContext.Current.CancellationToken);
        var date = _anchor.AddDays(5);

        var first = new PooledBondValuation(_context, date).ValueOn(position, date);
        var second = new PooledBondValuation(_context, date).ValueOn(position, date);

        Assert.Equal(first, second);
        Assert.Equal(first, _context.GetOrComputePrice(1, _anchor, 2m, date, () => throw new InvalidOperationException("Should be memoized")));
    }

    [Fact]
    public async Task LoadDetailsAsync_LoadsEveryPartitionOfAWalkedRangeOnce()
    {
        var requested = new List<int[]>();
        var repository = new Mock<IBondDetailsRepository>();
        repository.Setup(x => x.GetByIdsAsync(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<CancellationToken>()))
            .Callback((IReadOnlyCollection<int> ids, CancellationToken _) => requested.Add([.. ids.Order()]))
            .ReturnsAsync([_details]);
        var context = new BondDashboardContext(repository.Object);
        var walk = new OwnerLedgerWalk(new([new(1, 5m, _anchor.ToDateTime(TimeOnly.MinValue))], [new(2, 9, _anchor.ToDateTime(TimeOnly.MinValue), 1m)]));

        await PooledBondValuation.LoadDetailsAsync(context, walk, TestContext.Current.CancellationToken);
        await PooledBondValuation.LoadDetailsAsync(context, walk, TestContext.Current.CancellationToken);

        Assert.Equal([[1, 2]], requested);
    }

    private static BondDetails CreateDetails(int id, decimal unitValue, decimal rate) =>
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
            Id = id,
        };
}