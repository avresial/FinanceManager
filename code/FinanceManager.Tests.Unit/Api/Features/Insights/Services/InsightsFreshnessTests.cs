using FinanceManager.Api.Features.Insights.Services;

namespace FinanceManager.Tests.Unit.Api.Features.Insights.Services;

[Trait("Category", "Unit")]
public class InsightsFreshnessTests
{
    private static readonly DateTime _utcNow = new(2026, 8, 2, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void IsStale_ReturnsTrue_WhenThereAreNoInsights() =>
        Assert.True(InsightsFreshness.IsStale(null, _utcNow));

    [Fact]
    public void IsStale_ReturnsTrue_WhenNewestInsightIsOlderThanSevenDays() =>
        Assert.True(InsightsFreshness.IsStale(_utcNow.AddDays(-7).AddSeconds(-1), _utcNow));

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-6)]
    [InlineData(-7)]
    public void IsStale_ReturnsFalse_WithinSevenDays(int daysOld) =>
        Assert.False(InsightsFreshness.IsStale(_utcNow.AddDays(daysOld), _utcNow));
}