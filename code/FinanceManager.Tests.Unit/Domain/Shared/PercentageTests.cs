using FinanceManager.Domain.Shared;

namespace FinanceManager.Tests.Unit.Domain.Shared;

[Trait("Category", "Unit")]
public class PercentageTests
{
    [Theory]
    [InlineData(5, 50, 10)]
    [InlineData(-25, 100, -25)]
    [InlineData(30, -60, -50)]
    public void Of_ReturnsPartAsPercentOfWhole(decimal part, decimal whole, decimal expected) =>
        Assert.Equal(expected, Percentage.Of(part, whole));

    [Fact]
    public void Of_ReturnsNullWhenWholeIsZero() => Assert.Null(Percentage.Of(10m, 0m));
}