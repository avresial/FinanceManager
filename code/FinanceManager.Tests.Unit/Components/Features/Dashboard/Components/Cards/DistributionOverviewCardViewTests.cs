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
    public async Task Liabilities_SingleCategory_ShowsFigureWithoutChart()
    {
        await using var context = CreateContext();
        var cut = context.Render<LiabilitiesDistributionOverviewCardView>(p => p.Add(x => x.TypeData, _single));

        AssertSummary(cut);
        AssertSingleCategoryFigure(cut);
        Assert.Empty(cut.FindComponents<ApexChart<NameValueResult>>());
    }

    [Fact]
    public async Task Liabilities_TwoCategories_ShowsChart()
    {
        await using var context = CreateContext();
        var cut = context.Render<LiabilitiesDistributionOverviewCardView>(p => p.Add(x => x.TypeData, _two));

        Assert.Single(cut.FindComponents<ApexChart<NameValueResult>>());
        Assert.Empty(cut.FindAll("[class*=legend--single]"));
        Assert.Equal(2, cut.FindAll(".fm-legend-row").Count);
    }

    [Fact]
    public async Task Assets_SingleCategory_ShowsFigureWithoutChart()
    {
        await using var context = CreateContext();
        var cut = context.Render<AssetsDistributionOverviewCardView>(p => p.Add(x => x.TypeData, _single));

        AssertSummary(cut);
        AssertSingleCategoryFigure(cut);
        Assert.Empty(cut.FindComponents<ApexChart<NameValueResult>>());
    }

    [Fact]
    public async Task Assets_TwoCategories_ShowsChart()
    {
        await using var context = CreateContext();
        var cut = context.Render<AssetsDistributionOverviewCardView>(p => p.Add(x => x.TypeData, _two));

        Assert.Single(cut.FindComponents<ApexChart<NameValueResult>>());
        Assert.Empty(cut.FindAll("[class*=legend--single]"));
        Assert.Equal(2, cut.FindAll(".fm-legend-row").Count);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(75)]
    public async Task Assets_WalletToggle_UsesWalletTotal(decimal typeTotal)
    {
        await using var context = CreateContext();
        List<NameValueResult> typeData = typeTotal == 0 ? [] : [new("Stock", typeTotal)];
        var cut = context.Render<AssetsDistributionOverviewCardView>(p => p
            .Add(x => x.TypeData, typeData)
            .Add(x => x.WalletData, [new("Main wallet", 100m)]));

        cut.FindAll("button").Single(x => x.TextContent.Contains("Wallet")).Click();

        AssertSingleCategoryFigure(cut);
        Assert.Matches("100[.,]00", cut.Find(".fm-card-total").TextContent);
        Assert.Single(cut.FindAll(".fm-legend-row"));
        Assert.Contains("Main wallet", cut.Find(".fm-legend-name").TextContent);
        Assert.StartsWith("100", cut.Find(".fm-legend-pct").TextContent);
        Assert.DoesNotContain("No data", cut.Markup);

        cut.FindAll("button").Single(x => x.TextContent.Contains("Type")).Click();
        if (typeTotal == 0)
            Assert.Contains("No data", cut.Markup);
        else
            Assert.Matches("75[.,]00", cut.Find(".fm-card-total").TextContent);
    }

    [Fact]
    public async Task Expense_SingleCategory_ShowsFigureWithoutChart()
    {
        await using var context = CreateContext();
        var cut = context.Render<ExpenseDistributionOverviewCardView>(p => p.Add(x => x.Data, _single));

        AssertSingleCategoryFigure(cut);
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
        Assert.Empty(cut.FindAll("[class*=legend--single]"));
        Assert.Equal(2, cut.FindAll(".ed-legend-row").Count);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-100)]
    public async Task NonpositiveTotals_ShowNoData(decimal value)
    {
        await using var context = CreateContext();
        List<NameValueResult> data = [new("Mortgage", value)];
        var liabilities = context.Render<LiabilitiesDistributionOverviewCardView>(p => p.Add(x => x.TypeData, data));
        var assets = context.Render<AssetsDistributionOverviewCardView>(p => p.Add(x => x.TypeData, data));
        var expenses = context.Render<ExpenseDistributionOverviewCardView>(p => p.Add(x => x.Data, data));

        foreach (var cut in new IRenderedComponent<IComponent>[] { liabilities, assets, expenses })
        {
            Assert.Contains("No data", cut.Markup);
            Assert.Empty(cut.FindComponents<ApexChart<NameValueResult>>());
        }
    }

    [Fact]
    public async Task Liabilities_AccountToggle_SwitchesBetweenFigureAndChart()
    {
        await using var context = CreateContext();
        var cut = context.Render<LiabilitiesDistributionOverviewCardView>(p => p
            .Add(x => x.TypeData, _single).Add(x => x.AccountData, _two));
        AssertSingleCategoryFigure(cut);

        cut.FindAll("button").Single(x => x.TextContent.Contains("Account")).Click();
        Assert.Single(cut.FindComponents<ApexChart<NameValueResult>>());
        Assert.Empty(cut.FindAll("[class*=legend--single]"));

        cut.FindAll("button").Single(x => x.TextContent.Contains("Type")).Click();
        AssertSingleCategoryFigure(cut);
        Assert.Empty(cut.FindComponents<ApexChart<NameValueResult>>());
    }

    private static void AssertSingleCategoryFigure(IRenderedComponent<IComponent> cut)
    {
        var legend = cut.Find("[data-testid=distribution-legend]");
        Assert.Contains("legend--single", legend.ClassName);
        Assert.Matches("100[.,]00", legend.TextContent);
        Assert.Contains("100.0%", legend.TextContent.Replace(",", "."));
        Assert.Empty(cut.FindComponents<ApexChart<NameValueResult>>());
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