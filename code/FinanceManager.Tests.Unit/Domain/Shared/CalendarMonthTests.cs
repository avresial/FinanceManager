using FinanceManager.Domain.Shared;

namespace FinanceManager.Tests.Unit.Domain.Shared;

[Trait("Category", "Unit")]
public class CalendarMonthTests
{
    [Fact]
    public void Of_DateTime_IgnoresDayAndTime()
    {
        var month = CalendarMonth.Of(new DateTime(2024, 2, 29, 23, 59, 59, DateTimeKind.Local));

        Assert.Equal(new CalendarMonth(2024, 2), month);
    }

    [Fact]
    public void Of_DateOnly_MatchesDateTime() =>
        Assert.Equal(CalendarMonth.Of(new DateTime(2024, 7, 15)), CalendarMonth.Of(new DateOnly(2024, 7, 31)));

    [Fact]
    public void Start_DefaultsToUtcMidnightOfFirstDay()
    {
        var start = new CalendarMonth(2024, 3).Start();

        Assert.Equal(new DateTime(2024, 3, 1, 0, 0, 0, DateTimeKind.Utc), start);
        Assert.Equal(DateTimeKind.Utc, start.Kind);
    }

    [Fact]
    public void Start_KeepsRequestedKind() =>
        Assert.Equal(DateTimeKind.Unspecified, new CalendarMonth(2024, 3).Start(DateTimeKind.Unspecified).Kind);

    [Theory]
    [InlineData(2024, 2, 29)] // leap year
    [InlineData(2023, 2, 28)]
    [InlineData(2024, 12, 31)] // year rollover
    public void EndInclusive_IsLastTickOfLastDay(int year, int month, int lastDay)
    {
        var end = new CalendarMonth(year, month).EndInclusive();

        Assert.Equal(new DateTime(year, month, lastDay, 0, 0, 0, DateTimeKind.Utc).AddDays(1).AddTicks(-1), end);
        Assert.Equal(DateTimeKind.Utc, end.Kind);
    }

    [Fact]
    public void FirstDay_IsFirstOfMonth() =>
        Assert.Equal(new DateOnly(2024, 11, 1), new CalendarMonth(2024, 11).FirstDay);

    [Theory]
    [InlineData(1, 2025, 1)]
    [InlineData(-12, 2023, 12)]
    [InlineData(0, 2024, 12)]
    public void AddMonths_CrossesYearBoundaries(int months, int expectedYear, int expectedMonth) =>
        Assert.Equal(new CalendarMonth(expectedYear, expectedMonth), new CalendarMonth(2024, 12).AddMonths(months));

    [Theory]
    [InlineData(2024, 0)]
    [InlineData(2024, 13)]
    [InlineData(0, 1)]
    public void Constructor_RejectsInvalidMonth(int year, int month) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new CalendarMonth(year, month));
}