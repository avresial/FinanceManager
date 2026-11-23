using FinanceManager.Domain.Transactions.Shared;
using FinanceManager.Domain.Transactions.Shared.Dtos;

namespace FinanceManager.Tests.Unit.Domain.Transactions;

[Trait("Category", "Unit")]
public class OwnerLedgerWalkTests
{
    private static readonly DateTime _day = new(2024, 1, 10, 0, 0, 0, DateTimeKind.Utc);

    private static OwnerLedgerPosition Opening(int partition, decimal value, DateTime lastPosting) => new(partition, value, lastPosting);

    private static OwnerLedgerPoint Point(int partition, long entryId, DateTime posting, decimal change) => new(partition, entryId, posting, change);

    [Fact]
    public void At_StartsFromTheOpeningBalanceAndAddsEntriesPostedOnOrBeforeTheInstant()
    {
        var walk = new OwnerLedgerWalk(new(
            [Opening(1, 100m, _day.AddDays(-3))],
            [Point(1, 1, _day.AddHours(8), 20m), Point(1, 2, _day.AddHours(18), -50m)]));

        Assert.Equal([new OwnerLedgerPosition(1, 100m, _day.AddDays(-3))], walk.At(_day));
        Assert.Equal([new OwnerLedgerPosition(1, 120m, _day.AddHours(8))], walk.At(_day.AddHours(8)));
        Assert.Equal([new OwnerLedgerPosition(1, 70m, _day.AddHours(18))], walk.At(_day.AddHours(23)));
    }

    [Fact]
    public void At_ReadsALocalInstantAsTheUtcMomentItIs()
    {
        var walk = new OwnerLedgerWalk(new([], [Point(1, 1, _day.AddHours(8), 20m)]));
        var localBeforePosting = _day.AddHours(7).ToLocalTime();
        var localAfterPosting = _day.AddHours(9).ToLocalTime();

        Assert.Empty(walk.At(localBeforePosting));
        Assert.Equal([new OwnerLedgerPosition(1, 20m, _day.AddHours(8))], walk.At(localAfterPosting));
    }

    [Fact]
    public void At_OmitsPartitionsThatHaveNoEntryYet_AndOrdersByPartition()
    {
        var walk = new OwnerLedgerWalk(new(
            [Opening(5, 1m, _day.AddDays(-1))],
            [Point(2, 1, _day.AddHours(10), 3m)]));

        Assert.Equal([5], walk.At(_day.AddHours(9)).Select(p => p.PartitionId));
        Assert.Equal([2, 5], walk.At(_day.AddHours(11)).Select(p => p.PartitionId));
    }

    [Fact]
    public void PositionAt_ReadsOnePartitionAndIsNullBeforeItsFirstEntry()
    {
        var walk = new OwnerLedgerWalk(new(
            [Opening(1, 100m, _day.AddDays(-3))],
            [Point(1, 1, _day.AddHours(8), 20m), Point(2, 2, _day.AddHours(9), 5m)]));

        Assert.Equal(new OwnerLedgerPosition(1, 100m, _day.AddDays(-3)), walk.PositionAt(1, _day));
        Assert.Equal(new OwnerLedgerPosition(1, 120m, _day.AddHours(8)), walk.PositionAt(1, _day.AddHours(23)));
        Assert.Null(walk.PositionAt(2, _day.AddHours(8)));
        Assert.Equal(new OwnerLedgerPosition(2, 5m, _day.AddHours(9)), walk.PositionAt(2, _day.AddHours(9)));
        Assert.Null(walk.PositionAt(3, _day.AddDays(1)));
    }

    [Fact]
    public void At_KeepsNettedToZeroPartitionsWithTheirLastPostingDate()
    {
        var walk = new OwnerLedgerWalk(new([], [Point(1, 1, _day, 10m), Point(1, 2, _day.AddHours(1), -10m)]));

        Assert.Equal([new OwnerLedgerPosition(1, 0m, _day.AddHours(1))], walk.At(_day.AddDays(1)));
    }

    [Fact]
    public void At_AppliesEntriesWithTheSameInstantInEntryIdOrder_AndReportsTheirPostingDate()
    {
        var walk = new OwnerLedgerWalk(new([], [Point(1, 7, _day, 5m), Point(1, 3, _day, 2m)]));

        Assert.Equal([new OwnerLedgerPosition(1, 7m, _day)], walk.At(_day));
    }

    [Fact]
    public void EachDayEnd_ValuesEveryCalendarDayAtItsEnd()
    {
        var walk = new OwnerLedgerWalk(new([], [Point(1, 1, _day.AddDays(1).AddHours(23), 4m)]));

        var days = walk.EachDayEnd(_day.AddHours(5), _day.AddDays(2).AddHours(1)).ToList();

        Assert.Equal([_day, _day.AddDays(1), _day.AddDays(2)], days.Select(d => d.Day));
        Assert.Equal([0, 1, 1], days.Select(d => d.Positions.Count));
        Assert.Equal(4m, days[2].Positions.Single().Value);
    }

    [Fact]
    public void IsEmpty_IsTrueOnlyWithoutOpeningAndPoints()
    {
        Assert.True(new OwnerLedgerWalk(OwnerLedgerRange.Empty).IsEmpty);
        Assert.False(new OwnerLedgerWalk(new([Opening(1, 0m, _day)], [])).IsEmpty);
        Assert.False(new OwnerLedgerWalk(new([], [Point(1, 1, _day, 1m)])).IsEmpty);
    }

    [Fact]
    public void PartitionIds_ListsEveryPartitionOfTheRange()
    {
        var walk = new OwnerLedgerWalk(new([Opening(1, 1m, _day)], [Point(2, 1, _day, 1m)]));

        Assert.Equal([1, 2], walk.PartitionIds.Order());
    }
}