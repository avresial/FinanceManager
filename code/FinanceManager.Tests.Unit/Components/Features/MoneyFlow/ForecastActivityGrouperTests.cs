using FinanceManager.Components.Features.MoneyFlow.Models;
using FinanceManager.Domain.MoneyFlow.Entities;

namespace FinanceManager.Tests.Unit.Components.Features.MoneyFlow;

[Trait("Category", "Unit")]
public class ForecastActivityGrouperTests
{
    private static CashFlowForecastTransaction Tx(int month, int day, decimal amount) =>
        new() { Date = new DateTime(2026, month, day), Amount = amount, Description = $"{month}/{day}" };

    [Fact]
    public void Group_OrdersByDateAndGroupsByMonth()
    {
        var months = ForecastActivityGrouper.Group([Tx(7, 3, -10m), Tx(6, 28, 50m), Tx(6, 5, -5m)], 100m);

        Assert.Equal(2, months.Count);
        Assert.Equal(new DateTime(2026, 6, 1), months[0].Month);
        Assert.Equal(["6/5", "6/28"], months[0].Rows.Select(r => r.Transaction.Description));
        Assert.Equal(45m, months[0].Net);
        Assert.Equal(-10m, months[1].Net);
    }

    [Fact]
    public void Group_ComputesRunningBalanceAcrossMonths()
    {
        var months = ForecastActivityGrouper.Group([Tx(6, 5, -5m), Tx(6, 28, 50m), Tx(7, 3, -10m)], 100m);

        Assert.Equal([95m, 145m], months[0].Rows.Select(r => r.RunningBalance));
        Assert.Equal([135m], months[1].Rows.Select(r => r.RunningBalance));
    }

    [Fact]
    public void Group_WithoutStartingBalance_LeavesRunningBalanceEmpty()
    {
        var months = ForecastActivityGrouper.Group([Tx(6, 5, -5m)], null);

        Assert.Null(months[0].Rows[0].RunningBalance);
    }

    [Fact]
    public void StartingBalance_PrefersFirstForecastPointThenLastHistoricalPoint()
    {
        var forecast = new CashFlowForecast
        {
            HistoricalSeries = [new(new DateTime(2026, 5, 1), 80m, "h"), new(new DateTime(2026, 6, 1), 90m, "h")],
            ForecastSeries = [new(new DateTime(2026, 6, 10), 120m, "f"), new(new DateTime(2026, 6, 2), 100m, "f")]
        };

        Assert.Equal(100m, ForecastActivityGrouper.StartingBalance(forecast));

        forecast.ForecastSeries = [];
        Assert.Equal(90m, ForecastActivityGrouper.StartingBalance(forecast));

        forecast.HistoricalSeries = [];
        Assert.Null(ForecastActivityGrouper.StartingBalance(forecast));
    }
}