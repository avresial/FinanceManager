using ApexCharts;
using Bunit;
using FinanceManager.Components.Features.Dashboard.Components.Cards;
using FinanceManager.Components.Features.Dashboard.Components.Cards.Assets;
using FinanceManager.Components.Features.Dashboard.Components.Cards.Liabilities;
using FinanceManager.Domain.MoneyFlow.Entities;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;

namespace FinanceManager.Tests.Unit.Components.Features.Dashboard.Components.Cards;

[Trait("Category", "Unit")]
public class DistributionOverviewCardViewTests
{
    private static readonly List<NameValueResult> _single = [new("Mortgage", 100m)];
    private static readonly List<NameValueResult> _two = [new("Mortgage", 75m), new("Loan", 25m)];

    [Fact]
    public async Task Liabilities_SingleCategory_ShowsSummaryWithoutChart()
    {
        await using var context = CreateContext();
        var cut = context.Render<LiabilitiesDistributionOverviewCardView>(p => p.Add(x => x.TypeData, _single));

        AssertSummary(cut);
        Assert.Empty(cut.FindComponents<ApexChart<NameValueResult>>());
    }

    [Fact]
    public async Task Liabilities_TwoCategories_ShowsChart()
    {
        await using var context = CreateContext();
        var cut = context.Render<LiabilitiesDistributionOverviewCardView>(p => p.Add(x => x.TypeData, _two));

        Assert.Single(cut.FindComponents<ApexChart<NameValueResult>>());
        Assert.Equal(2, cut.FindAll(".fm-legend-row").Count);
    }

    [Fact]
    public async Task Assets_SingleCategory_ShowsSummaryWithoutChart()
    {
        await using var context = CreateContext();
        var cut = context.Render<AssetsDistributionOverviewCardView>(p => p.Add(x => x.TypeData, _single));

        AssertSummary(cut);
        Assert.Empty(cut.FindComponents<ApexChart<NameValueResult>>());
    }

    [Fact]
    public async Task Assets_TwoCategories_ShowsChart()
    {
        await using var context = CreateContext();
        var cut = context.Render<AssetsDistributionOverviewCardView>(p => p.Add(x => x.TypeData, _two));

        Assert.Single(cut.FindComponents<ApexChart<NameValueResult>>());
        Assert.Equal(2, cut.FindAll(".fm-legend-row").Count);
    }

    [Fact]
    public async Task Expense_SingleCategory_ShowsSummaryWithoutChart()
    {
        await using var context = CreateContext();
        var cut = context.Render<ExpenseDistributionOverviewCardView>(p => p.Add(x => x.Data, _single));

        Assert.Single(cut.FindAll(".ed-legend-row"));
        Assert.Contains("Mortgage", cut.Find(".ed-legend-name").TextContent);
        Assert.Matches("100[.,]00", cut.Find(".ed-legend-amt").TextContent);
        Assert.StartsWith("100", cut.Find(".ed-legend-pct").TextContent);
        Assert.Empty(cut.FindComponents<ApexChart<NameValueResult>>());
    }

    [Fact]
    public async Task Expense_TwoCategories_ShowsChart()
    {
        await using var context = CreateContext();
        var cut = context.Render<ExpenseDistributionOverviewCardView>(p => p.Add(x => x.Data, _two));

        Assert.Single(cut.FindComponents<ApexChart<NameValueResult>>());
        Assert.Equal(2, cut.FindAll(".ed-legend-row").Count);
    }

    private static void AssertSummary(IRenderedComponent<IComponent> cut)
    {
        Assert.Single(cut.FindAll(".fm-legend-row"));
        Assert.Contains("Mortgage", cut.Find(".fm-legend-name").TextContent);
        Assert.Matches("100[.,]00", cut.Find(".fm-legend-amt").TextContent);
        Assert.StartsWith("100", cut.Find(".fm-legend-pct").TextContent);
    }

    private static BunitContext CreateContext()
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddMudServices();
        return context;
    }
}