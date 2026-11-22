using FinanceManager.Domain.Shared;

namespace FinanceManager.Tests.Unit.Domain.Shared;

[Trait("Category", "Unit")]
public class UtcDayTests
{
    private static readonly DateTime _now = new(2026, 11, 21, 9, 30, 0, DateTimeKind.Utc);
    private static readonly DateTime _endOfToday = new DateTime(2026, 11, 22, 0, 0, 0, DateTimeKind.Utc).AddTicks(-1);

    [Fact]
    public void EndOf_ReturnsLastTickOfTheDayAsUtc()
    {
        var end = UtcDay.EndOf(_now);

        Assert.Equal(_endOfToday, end);
        Assert.Equal(DateTimeKind.Utc, end.Kind);
    }
}