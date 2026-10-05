using FinanceManager.Components.Shared.Helpers;
using System.Globalization;

namespace FinanceManager.Tests.Unit.Components.Shared.Helpers;

[Trait("Category", "Unit")]
public class DateFormatterTests
{
    [Fact]
    public void Format_UsesDayMonthNameYear_WithoutLeadingZero()
    {
        Assert.Equal("4 Oct 2026", DateFormatter.Format(new DateTime(2026, 10, 4)));
        Assert.Equal("5 Oct 2026", DateFormatter.Format(new DateOnly(2026, 10, 5)));
        Assert.Equal("4 Oct", DateFormatter.FormatDayMonth(new DateTime(2026, 10, 4)));
        Assert.Equal("Sunday", DateFormatter.FormatWeekday(new DateTime(2026, 10, 4)));
        Assert.Equal("Oct 2026", DateFormatter.FormatMonthYear(new DateTime(2026, 10, 4)));
        Assert.Equal("4 Oct 2026, 14:30", DateFormatter.FormatDateTime(new DateTime(2026, 10, 4, 14, 30, 0)));
    }

    [Theory]
    [InlineData("2026-10-04", "2026-10-04", "4 Oct 2026")]
    [InlineData("2026-10-04", "2026-10-20", "4–20 Oct 2026")]
    [InlineData("2026-09-04", "2026-10-04", "4 Sep – 4 Oct 2026")]
    [InlineData("2025-12-30", "2026-01-02", "30 Dec 2025 – 2 Jan 2026")]
    public void FormatRange_CollapsesSharedParts(string start, string end, string expected)
    {
        Assert.Equal(expected, DateFormatter.FormatRange(DateTime.Parse(start, CultureInfo.InvariantCulture), DateTime.Parse(end, CultureInfo.InvariantCulture)));
        Assert.Equal(expected, DateFormatter.FormatRange(DateOnly.Parse(start, CultureInfo.InvariantCulture), DateOnly.Parse(end, CultureInfo.InvariantCulture)));
    }

    [Fact]
    public void Format_IgnoresCurrentCulture_AndNeverOutputsNumericDayMonth()
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("pl-PL");
            Assert.Equal("4 Sep – 4 Oct 2026", DateFormatter.FormatRange(new DateTime(2026, 9, 4), new DateTime(2026, 10, 4)));
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }
}