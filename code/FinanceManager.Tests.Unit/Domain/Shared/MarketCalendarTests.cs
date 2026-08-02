using FinanceManager.Domain.Shared;

namespace FinanceManager.Tests.Unit.Domain.Shared;

[Trait("Category", "Unit")]
public class MarketCalendarTests
{
    // 2024-06-03 is a Monday, so the week runs Mon 3rd … Sun 9th.
    private static readonly DateTime _monday = new(2024, 6, 3);
    private static readonly DateTime _friday = new(2024, 6, 7);
    private static readonly DateTime _saturday = new(2024, 6, 8);
    private static readonly DateTime _sunday = new(2024, 6, 9);
    private static readonly DateTime _nextMonday = new(2024, 6, 10);

    [Theory]
    [InlineData("2024-06-03", true)]  // Monday
    [InlineData("2024-06-07", true)]  // Friday
    [InlineData("2024-06-08", false)] // Saturday
    [InlineData("2024-06-09", false)] // Sunday
    public void IsMarketDay_IsTrueOnWeekdaysOnly(string date, bool expected) =>
        Assert.Equal(expected, MarketCalendar.IsMarketDay(DateTime.Parse(date)));

    [Fact]
    public void LastMarketDay_MapsWeekendBackToPrecedingFriday()
    {
        Assert.Equal(_friday, MarketCalendar.LastMarketDay(_saturday));
        Assert.Equal(_friday, MarketCalendar.LastMarketDay(_sunday));
    }

    [Fact]
    public void LastMarketDay_LeavesWeekdaysUnchanged()
    {
        Assert.Equal(_monday, MarketCalendar.LastMarketDay(_monday));
        Assert.Equal(_friday, MarketCalendar.LastMarketDay(_friday));
    }

    [Fact]
    public void LastMarketDay_DropsTheTimeComponent() =>
        Assert.Equal(_friday, MarketCalendar.LastMarketDay(_sunday.AddHours(23).AddMinutes(59)));

    [Fact]
    public void NextMarketDay_MapsWeekendForwardToFollowingMonday()
    {
        Assert.Equal(_nextMonday, MarketCalendar.NextMarketDay(_saturday));
        Assert.Equal(_nextMonday, MarketCalendar.NextMarketDay(_sunday));
    }

    [Fact]
    public void NextMarketDay_LeavesWeekdaysUnchanged() =>
        Assert.Equal(_friday, MarketCalendar.NextMarketDay(_friday));

    [Fact]
    public void HasMarketDay_IsFalseForWeekendOnlyWindows()
    {
        Assert.False(MarketCalendar.HasMarketDay(_saturday, _sunday));
        Assert.False(MarketCalendar.HasMarketDay(_saturday, _saturday));
        Assert.False(MarketCalendar.HasMarketDay(_sunday, _sunday));
    }

    [Fact]
    public void HasMarketDay_IsTrueWhenTheWindowReachesAWeekday()
    {
        Assert.True(MarketCalendar.HasMarketDay(_friday, _sunday));
        Assert.True(MarketCalendar.HasMarketDay(_saturday, _nextMonday));
        Assert.True(MarketCalendar.HasMarketDay(_monday, _monday));
    }

    [Fact]
    public void HasMarketDay_IsFalseForAnInvertedWindow() =>
        Assert.False(MarketCalendar.HasMarketDay(_nextMonday, _monday));
}