using FinanceManager.Components.Shared.Helpers;

namespace FinanceManager.Tests.Unit.Components.Shared.Helpers;

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
    public void GetRange_ResolvesPresetDeterministically(string selection, int year, int month, int day)
    {
        var range = DateRangeHelper.GetRange(selection, null, null, _now.AddMonths(-3), _now.AddYears(-1), _now);

        var expected = new DateTime(year, month, day, 0, 0, 0, DateTimeKind.Utc);
        if (selection is not ("Month" or "YTD")) expected = expected.AddHours(_now.Hour);
        Assert.Equal(expected, range.Start);
        Assert.Equal(_now, range.End);
    }

    [Fact]
    public void GetRange_CustomRangeClampsFutureEnd()
    {
        var start = _now.AddDays(-10);
        var range = DateRangeHelper.GetRange(DateRangeHelper.CustomRangeKey, start, _now.AddDays(1), _now.AddMonths(-3), _now, _now);

        Assert.Equal(start, range.Start);
        Assert.Equal(_now, range.End);
    }

    // Pickers hand over midnight for the end day; the selected day itself must still be included.
    [Fact]
    public void GetRange_CustomRangeIncludesWholeEndDay()
    {
        var start = _now.AddMonths(-4);
        var end = _now.AddMonths(-3).Date;
        var range = DateRangeHelper.GetRange(DateRangeHelper.CustomRangeKey, start, end, _now.AddMonths(-3), _now, _now);

        Assert.Equal(start, range.Start);
        Assert.Equal(end.AddDays(1).AddTicks(-1), range.End);
    }

    // The dashboard seeds a rolling 31-day window (counting today) anchored to midnight, so the
    // start is 30 days before today and the end is exactly `now`.
    [Fact]
    public void GetLast31DaysRange_ReturnsMidnightStart30DaysBackAndNowEnd()
    {
        var range = DateRangeHelper.GetLast31DaysRange(_now);

        Assert.Equal(new DateTime(2026, 6, 12, 0, 0, 0, DateTimeKind.Utc), range.Start);
        Assert.Equal(_now, range.End);
    }

    // Dashboard, Assets and Liabilities all seed their initial range from this one entry point,
    // so it must resolve to the same window for every caller. #700
    [Fact]
    public void GetDefaultOverviewRange_MatchesLast31DaysRange()
    {
        Assert.Equal(DateRangeHelper.GetLast31DaysRange(_now), DateRangeHelper.GetDefaultOverviewRange(_now));
    }

    // The dashboard keeps the 31-day start but ends at the last tick of today (UTC), so entries
    // posted later today still count. #1120
    [Fact]
    public void GetDefaultDashboardRange_EndsAtEndOfTodayUtc()
    {
        var range = DateRangeHelper.GetDefaultDashboardRange(_now);

        Assert.Equal(DateRangeHelper.GetDefaultOverviewRange(_now).Start, range.Start);
        Assert.Equal(new DateTime(2026, 7, 13, 0, 0, 0, DateTimeKind.Utc).AddTicks(-1), range.End);
        Assert.Equal(DateTimeKind.Utc, range.End.Kind);
    }

    // Every visit on the same UTC day asks for the identical range, so the server's overview cache
    // key does not change with the time of day. #1120
    [Fact]
    public void GetDefaultDashboardRange_IsStableWithinTheSameUtcDay()
    {
        var firstVisit = DateRangeHelper.GetDefaultDashboardRange(_now.Date);
        var lastVisit = DateRangeHelper.GetDefaultDashboardRange(_now.Date.AddDays(1).AddTicks(-1));

        Assert.Equal(firstVisit, DateRangeHelper.GetDefaultDashboardRange(_now));
        Assert.Equal(firstVisit, lastVisit);
    }

    [Fact]
    public void GetDefaultDashboardRange_RollsOverAtUtcMidnight()
    {
        var today = DateRangeHelper.GetDefaultDashboardRange(_now.Date.AddDays(1).AddTicks(-1));
        var tomorrow = DateRangeHelper.GetDefaultDashboardRange(_now.Date.AddDays(1));

        Assert.Equal(today.Start.AddDays(1), tomorrow.Start);
        Assert.Equal(today.End.AddDays(1), tomorrow.End);
    }

    [Fact]
    public void GetExpandedStart_ReturnsOnlyOlderEntry()
    {
        Assert.Equal(_now.AddDays(-1), DateRangeHelper.GetExpandedStart(_now, _now.AddDays(-1)));
        Assert.Null(DateRangeHelper.GetExpandedStart(_now, _now));
    }
}