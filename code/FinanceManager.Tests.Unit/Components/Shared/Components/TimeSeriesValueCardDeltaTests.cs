using Bunit;
using FinanceManager.Components.Shared.Components.Charts;
using FinanceManager.Domain.MoneyFlow.Entities;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;

namespace FinanceManager.Tests.Unit.Components.Shared.Components;

[Trait("Category", "Unit")]
public class TimeSeriesValueCardDeltaTests
{
    private const string _green = "#66BB6A";
    private const string _red = "#EF5350";
    private static readonly DateTime _start = new(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Debt_Decreasing_ShowsDownArrowOnDebtAmountInGreen()
    {
        // -22,000 -> -21,600: the balance rose, but the debt fell by 400.
        await using var context = CreateContext();

        var cut = RenderCard(context, isDebt: true, -22_000m, -21_600m);

        var delta = cut.Find(".fm-tsvc-delta");
        Assert.Equal("Debt ▼ 400.00 PLN", delta.TextContent);
        Assert.Contains(_green, delta.GetAttribute("style"));
    }

    [Fact]
    public async Task Debt_Increasing_ShowsUpArrowOnDebtAmountInRed()
    {
        await using var context = CreateContext();

        var cut = RenderCard(context, isDebt: true, -22_000m, -22_500m);

        var delta = cut.Find(".fm-tsvc-delta");
        Assert.Equal("Debt ▲ 500.00 PLN", delta.TextContent);
        Assert.Contains(_red, delta.GetAttribute("style"));
    }

    [Fact]
    public async Task Debt_Unchanged_ShowsNeutralWordingInGreen()
    {
        await using var context = CreateContext();

        var cut = RenderCard(context, isDebt: true, -22_000m, -22_000m);

        var delta = cut.Find(".fm-tsvc-delta");
        Assert.Equal("Debt unchanged", delta.TextContent);
        Assert.Contains(_green, delta.GetAttribute("style"));
    }

    [Fact]
    public async Task NonDebt_KeepsPercentageBadge()
    {
        await using var context = CreateContext();

        var cut = RenderCard(context, isDebt: false, 1_000m, 1_100m);

        var delta = cut.Find(".fm-tsvc-delta");
        Assert.Equal("▲ +10.0 %", delta.TextContent);
        Assert.Contains(_green, delta.GetAttribute("style"));
    }

    private static BunitContext CreateContext()
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddLogging();
        context.Services.AddMudServices();
        return context;
    }

    private static IRenderedComponent<TimeSeriesValueCard> RenderCard(
        BunitContext context, bool isDebt, decimal first, decimal last) =>
        context.Render<TimeSeriesValueCard>(parameters => parameters
            .Add(card => card.Title, "Liabilities value over time")
            .Add(card => card.IsDebt, isDebt)
            .Add(card => card.Data, [new TimeSeriesModel(_start, first), new TimeSeriesModel(_start.AddMonths(1), last)]));
}