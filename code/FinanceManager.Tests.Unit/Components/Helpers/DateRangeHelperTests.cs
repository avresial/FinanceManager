using FinanceManager.Components.Helpers;

namespace FinanceManager.Tests.Unit.Components.Helpers;

[Trait("Category", "Unit")]
public class DateRangeHelperTests
{
    private static readonly DateTime _now = new(2026, 7, 12, 14, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData("Month", 2026, 7, 1)]
    [InlineData("1M", 2026, 6, 12)]
    [InlineData("3M", 2026, 4, 12)]
    [InlineData("6M", 2026, 1, 12)]
    [InlineData("YTD", 2026, 1, 1)]
    public void GetAccountDetailsRange_ResolvesPresetDeterministically(string selection, int year, int month, int day)
    {
        var range = DateRangeHelper.GetAccountDetailsRange(selection, null, null, _now.AddMonths(-3), _now.AddYears(-1), _now);

        var expected = new DateTime(year, month, day, 0, 0, 0, DateTimeKind.Utc);
        if (selection is not ("Month" or "YTD")) expected = expected.AddHours(_now.Hour);
        Assert.Equal(expected, range.Start);
        Assert.Equal(_now, range.End);
    }

    [Fact]
    public void GetAccountDetailsRange_CustomRangeClampsFutureEnd()
    {
        var start = _now.AddDays(-10);
        var range = DateRangeHelper.GetAccountDetailsRange("Custom", start, _now.AddDays(1), _now.AddMonths(-3), _now, _now);

        Assert.Equal(start, range.Start);
        Assert.Equal(_now, range.End);
    }

    [Fact]
    public void GetExpandedStart_ReturnsOnlyOlderEntry()
    {
        Assert.Equal(_now.AddDays(-1), DateRangeHelper.GetExpandedStart(_now, _now.AddDays(-1)));
        Assert.Null(DateRangeHelper.GetExpandedStart(_now, _now));
    }
}