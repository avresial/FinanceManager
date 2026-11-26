using ApexCharts;
using Bunit;
using FinanceManager.Components.Features.Dashboard.Components.Cards;
using FinanceManager.Components.Features.Dashboard.Components.Cards.Assets;
using FinanceManager.Components.Features.Dashboard.Components.Cards.Liabilities;
using FinanceManager.Components.Features.Dashboard.Models;
using FinanceManager.Components.Shared.Components;
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
    public async Task DistributionCards_OptIntoPhoneAutoHeight_AndKeepSuppliedHeight()
    {
        await using var context = CreateContext();
        var assets = context.Render<AssetsDistributionOverviewCardView>(p => p.Add(x => x.TypeData, _single).Add(x => x.Height, "411px"));
        var liabilities = context.Render<LiabilitiesDistributionOverviewCardView>(p => p.Add(x => x.TypeData, _single).Add(x => x.Height, "411px"));
        var expenses = context.Render<ExpenseDistributionOverviewCardView>(p => p.Add(x => x.Data, _single).Add(x => x.Height, "411px"));

        AssertPhoneAutoHeight(assets, "411px");
        AssertPhoneAutoHeight(liabilities, "411px");
        AssertPhoneAutoHeight(expenses, "411px");
    }

    [Fact]
    public async Task Liabilities_SingleCategory_ShowsFigureWithoutChart()
    {
        await using var context = CreateContext();
        var cut = context.Render<LiabilitiesDistributionOverviewCardView>(p => p.Add(x => x.TypeData, _single));

        AssertSummary(cut);
        AssertSingleCategoryFigure(cut);
        Assert.Empty(cut.FindComponents<ApexChart<DistributionSlice>>());
    }

    [Fact]
    public async Task Liabilities_TwoCategories_ShowsChart()
    {
        await using var context = CreateContext();
        var cut = context.Render<LiabilitiesDistributionOverviewCardView>(p => p.Add(x => x.TypeData, _two));

        Assert.Single(cut.FindComponents<ApexChart<DistributionSlice>>());
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
        Assert.Empty(cut.FindComponents<ApexChart<DistributionSlice>>());
    }

    [Fact]
    public async Task Assets_TwoCategories_ShowsChart()
    {
        await using var context = CreateContext();
        var cut = context.Render<AssetsDistributionOverviewCardView>(p => p.Add(x => x.TypeData, _two));

        Assert.Single(cut.FindComponents<ApexChart<DistributionSlice>>());
        Assert.Empty(cut.FindAll("[class*=legend--single]"));
        Assert.Equal(2, cut.FindAll(".fm-legend-row").Count);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(75)]
    public async Task Assets_BreakdownToggle_UsesBreakdownTotal(decimal typeTotal)
    {
        await using var context = CreateContext();
        List<NameValueResult> typeData = typeTotal == 0 ? [] : [new("Stock", typeTotal)];
        var cut = context.Render<AssetsDistributionOverviewCardView>(p => p
            .Add(x => x.TypeData, typeData)
            .Add(x => x.WalletData, [new("Main wallet", 100m)]));

        cut.FindAll("button").Single(x => x.TextContent.Contains("Holdings")).Click();

        cut.WaitForAssertion(() =>
        {
            AssertSingleCategoryFigure(cut);
            Assert.Single(cut.FindAll(".fm-legend-row"));
            Assert.Contains("Main wallet", cut.Find(".fm-legend-name").TextContent);
            Assert.StartsWith("100", cut.Find(".fm-legend-pct").TextContent);
            Assert.DoesNotContain("No data", cut.Markup);
        }, TimeSpan.FromSeconds(3));

        cut.FindAll("button").Single(x => x.TextContent.Contains("Type")).Click();
        cut.WaitForAssertion(() =>
        {
            if (typeTotal == 0)
                Assert.Contains("No data", cut.Markup);
            else
                Assert.Matches("75[.,]00", cut.Find("[data-testid=distribution-legend]").TextContent);
        }, TimeSpan.FromSeconds(3));
    }

    [Fact]
    public async Task Expense_SingleCategory_ShowsFigureWithoutChart()
    {
        await using var context = CreateContext();
        var cut = context.Render<ExpenseDistributionOverviewCardView>(p => p.Add(x => x.Data, _single));

        AssertSingleCategoryFigure(cut);
        Assert.Single(cut.FindAll(".fm-legend-row"));
        Assert.Contains("Mortgage", cut.Find(".fm-legend-name").TextContent);
        Assert.Matches("100[.,]00", cut.Find(".fm-legend-amt").TextContent);
        Assert.StartsWith("100", cut.Find(".fm-legend-pct").TextContent);
        Assert.Empty(cut.FindComponents<ApexChart<DistributionSlice>>());
    }

    [Fact]
    public async Task Expense_TwoCategories_ShowsDonutWithTotalInCentre()
    {
        await using var context = CreateContext();
        var cut = context.Render<ExpenseDistributionOverviewCardView>(p => p.Add(x => x.Data, _two));

        Assert.Single(cut.FindComponents<ApexChart<DistributionSlice>>());
        Assert.Empty(cut.FindAll("[class*=legend--single]"));
        Assert.Equal(2, cut.FindAll(".fm-legend-row").Count);
        Assert.Matches("100[.,]00", cut.Find("[data-testid=distribution-chart] [data-testid=distribution-total]").TextContent);
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
            Assert.Empty(cut.FindComponents<ApexChart<DistributionSlice>>());
        }
    }

    [Fact]
    public async Task AssetsAndLiabilities_HeaderHoldsToggle_AndSpinnerOnlyWhileLoading()
    {
        await using var context = CreateContext();
        var assets = context.Render<AssetsDistributionOverviewCardView>(p => p.Add(x => x.TypeData, _two));
        var liabilities = context.Render<LiabilitiesDistributionOverviewCardView>(p => p.Add(x => x.TypeData, _two));
        var loading = context.Render<AssetsDistributionOverviewCardView>(p => p.Add(x => x.IsLoading, true));

        AssertHeaderHasOnlyToggle(assets);
        AssertHeaderHasOnlyToggle(liabilities);
        AssertActionsPrecedeFigure(loading);
    }

    [Fact]
    public async Task TwoCategories_ShowTotalInDonutCentre_AndProportionBars()
    {
        await using var context = CreateContext();
        var cut = context.Render<AssetsDistributionOverviewCardView>(p => p.Add(x => x.TypeData, _two));

        Assert.Matches("100[.,]00", cut.Find("[data-testid=distribution-chart] [data-testid=distribution-total]").TextContent);
        var bars = cut.FindAll(".fm-distribution-bar-fill");
        Assert.Equal(2, bars.Count);
        Assert.Contains("width:75%", bars[0].GetAttribute("style"));
        Assert.Contains("width:25%", bars[1].GetAttribute("style"));
        Assert.NotEqual(BarColor(bars[0]), BarColor(bars[1]));
    }

    [Fact]
    public async Task CurrencyView_KeepsRowsPlain_EvenWhenSlicesCarryAccountIds()
    {
        await using var context = CreateContext();
        var cut = context.Render<LiabilitiesDistributionOverviewCardView>(p => p
            .Add(x => x.TypeData, [new("Loan", 75m) { AccountId = 3 }, new("Mortgage", 25m) { AccountId = 4 }])
            .Add(x => x.AccountData, [new("Car loan", 75m) { AccountId = 7 }, new("PLN", 25m)]));

        // Account pages are gone, so no row links to one.
        Assert.Empty(cut.FindAll("a[data-testid=distribution-row]"));

        cut.FindAll("button").Single(x => x.TextContent.Contains("Currency")).Click();

        // Switching views replaces the chart, so the new rows arrive after the click's render completes.
        cut.WaitForAssertion(() =>
        {
            Assert.Equal(2, cut.FindAll("[data-testid=distribution-row]").Count);
            Assert.Empty(cut.FindAll("a[data-testid=distribution-row]"));
            Assert.DoesNotContain("AccountDetails", cut.Markup);
        }, TimeSpan.FromSeconds(3));
    }

    [Fact]
    public async Task SlicesThatShareAName_RenderAsSeparateRows()
    {
        await using var context = CreateContext();
        var cut = context.Render<AssetsDistributionOverviewCardView>(p => p
            .Add(x => x.TypeData, [new("Cash", 60m), new("Cash", 40m)]));

        Assert.Equal(2, cut.FindAll("[data-testid=distribution-row]").Count);
    }

    [Fact]
    public async Task HoldingsRows_AreLabelledByKind_AndLinkToTheirTransactions()
    {
        await using var context = CreateContext();
        var cut = context.Render<AssetsDistributionOverviewCardView>(p => p
            .Add(x => x.TypeData, [new("Cash", 100m), new("Stock", 50m)])
            .Add(x => x.WalletData,
            [
                new("PLN", 100m) { Kind = "Cash", CurrencyCode = "PLN" },
                new("CSPX", 40m) { Kind = "Stock" },
                new("EDO 1235", 10m) { Kind = "Bond", CurrencyCode = "PLN" },
            ]));

        cut.FindAll("button").Single(x => x.TextContent.Contains("Holdings")).Click();

        cut.WaitForAssertion(() =>
        {
            var links = cut.FindAll("a[data-testid=distribution-row]");
            Assert.Equal(3, links.Count);
            Assert.Equal(["Cash \u00B7 PLN", "Stock \u00B7 CSPX", "Bond \u00B7 EDO 1235"], cut.FindAll(".fm-legend-name").Select(x => x.TextContent));
            Assert.Equal(["/Transactions?currency=PLN", "/Transactions?search=CSPX", "/Transactions?search=EDO%201235"], links.Select(x => x.GetAttribute("href")));
        }, TimeSpan.FromSeconds(3));
    }

    [Fact]
    public async Task HoldingsInDifferentCurrencies_ShowOwnCurrencies_WithoutTotalSharesOrChart()
    {
        await using var context = CreateContext();
        var cut = context.Render<AssetsDistributionOverviewCardView>(p => p
            .Add(x => x.TypeData, [new("Cash", 600m)])
            .Add(x => x.WalletData,
            [
                new("PLN", 500m) { Kind = "Cash", CurrencyCode = "PLN" },
                new("EUR", 100m) { Kind = "Cash", CurrencyCode = "EUR" },
            ]));

        cut.FindAll("button").Single(x => x.TextContent.Contains("Holdings")).Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Not converted", cut.Find("[data-testid=distribution-note]").TextContent);
            var amounts = cut.FindAll(".fm-legend-amt").Select(x => x.TextContent.Replace("\u00A0", " ")).ToList();
            Assert.Matches("500[.,]00 PLN", amounts[0]);
            Assert.Matches("100[.,]00 EUR", amounts[1]);
            Assert.Empty(cut.FindAll(".fm-legend-pct"));
            Assert.Empty(cut.FindAll("[data-testid=distribution-total]"));
            Assert.Empty(cut.FindComponents<ApexChart<DistributionSlice>>());
            // The list starts under the header instead of being centred in the fixed-height card.
            Assert.Contains("fm-distribution-container--top", cut.Find(".fm-distribution-container").ClassList);
        }, TimeSpan.FromSeconds(3));
    }

    [Fact]
    public async Task TwoCategories_KeepTheListCentredWithTheDonut()
    {
        await using var context = CreateContext();
        var cut = context.Render<AssetsDistributionOverviewCardView>(p => p
            .Add(x => x.TypeData, [new("Cash", 600m), new("Bonds", 400m)]));

        cut.WaitForAssertion(() =>
            Assert.DoesNotContain("fm-distribution-container--top", cut.Find(".fm-distribution-container").ClassList),
            TimeSpan.FromSeconds(3));
    }

    [Fact]
    public async Task TypeView_NamesTheCurrenciesItAddsUnconverted()
    {
        await using var context = CreateContext();
        var cut = context.Render<AssetsDistributionOverviewCardView>(p => p
            .Add(x => x.TypeData, [new("Cash", 600m), new("Stock", 400m)])
            .Add(x => x.WalletData,
            [
                new("PLN", 500m) { Kind = "Cash", CurrencyCode = "PLN" },
                new("EUR", 100m) { Kind = "Cash", CurrencyCode = "EUR" },
                new("CSPX", 400m) { Kind = "Stock" },
            ]));

        Assert.Equal("Includes EUR, not converted", cut.Find("[data-testid=unconverted-chip]").TextContent.Trim());
    }

    [Fact]
    public async Task OwnCurrencyOnly_ShowsNoNote()
    {
        await using var context = CreateContext();
        var cut = context.Render<AssetsDistributionOverviewCardView>(p => p
            .Add(x => x.TypeData, [new("Cash", 500m), new("Stock", 400m)])
            .Add(x => x.WalletData,
            [
                new("PLN", 500m) { Kind = "Cash", CurrencyCode = "PLN" },
                new("CSPX", 400m) { Kind = "Stock" },
            ]));

        Assert.Empty(cut.FindAll("[data-testid=distribution-note]"));
        Assert.Empty(cut.FindAll("[data-testid=unconverted-chip]"));
    }

    [Fact]
    public async Task LiabilitiesCurrencyView_ShowsASingleForeignCurrencyInItsOwnCode()
    {
        await using var context = CreateContext();
        var cut = context.Render<LiabilitiesDistributionOverviewCardView>(p => p
            .Add(x => x.TypeData, [new("Cash", 500m)])
            .Add(x => x.AccountData, [new("EUR", 500m) { CurrencyCode = "EUR" }]));

        Assert.Equal("Includes EUR, not converted", cut.Find("[data-testid=unconverted-chip]").TextContent.Trim());

        cut.FindAll("button").Single(x => x.TextContent.Contains("Currency")).Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Matches("500[.,]00 EUR", cut.Find(".fm-legend-amt").TextContent.Replace("\u00A0", " "));
            Assert.Empty(cut.FindAll("[data-testid=unconverted-chip]"));
            Assert.Equal("/Transactions?currency=EUR", cut.Find("a[data-testid=distribution-row]").GetAttribute("href"));
        }, TimeSpan.FromSeconds(3));
    }

    [Fact]
    public async Task ManyItems_GroupTailIntoExpandableOther()
    {
        await using var context = CreateContext();
        List<NameValueResult> data = [.. Enumerable.Range(1, 8).Select(i => new NameValueResult($"Item {i}", i))];
        var cut = context.Render<AssetsDistributionOverviewCardView>(p => p.Add(x => x.TypeData, data));

        var rows = cut.FindAll("[data-testid=distribution-row]");
        Assert.Equal(6, rows.Count);
        var other = cut.Find("button[data-testid=distribution-row]");
        Assert.Contains("Other", other.TextContent);
        Assert.Equal("false", other.GetAttribute("aria-expanded"));
        Assert.Empty(cut.FindAll("[data-testid=distribution-other-members]"));

        other.Click();

        var members = cut.WaitForElement("[data-testid=distribution-other-members]", TimeSpan.FromSeconds(3));
        Assert.Contains("Item 1", members.TextContent);
        Assert.Contains("Item 3", members.TextContent);
        Assert.Equal("true", cut.Find("button[data-testid=distribution-row]").GetAttribute("aria-expanded"));
    }

    [Fact]
    public async Task HoveringRow_HighlightsIt_UntilMouseLeaves()
    {
        await using var context = CreateContext();
        var cut = context.Render<AssetsDistributionOverviewCardView>(p => p.Add(x => x.TypeData, _two));

        cut.FindAll("[role=listitem]")[1].MouseEnter();

        // Hover handlers also call into the chart's JS, so let their render settle before asserting.
        cut.WaitForAssertion(() =>
        {
            var rows = cut.FindAll("[data-testid=distribution-row]");
            Assert.DoesNotContain("is-active", rows[0].ClassList);
            Assert.Contains("is-active", rows[1].ClassList);
        }, TimeSpan.FromSeconds(3));

        cut.FindAll("[role=listitem]")[1].MouseLeave();

        cut.WaitForAssertion(() => Assert.DoesNotContain(cut.FindAll("[data-testid=distribution-row]"), row => row.ClassList.Contains("is-active")),
            TimeSpan.FromSeconds(3));
    }

    [Fact]
    public async Task Expense_HeaderHasNoToggleOrFigure_LikeTheOtherDistributionCards()
    {
        await using var context = CreateContext();
        var expenses = context.Render<ExpenseDistributionOverviewCardView>(p => p.Add(x => x.Data, _two));

        // Expenses are grouped by label only, so there is no Type/Account toggle; the total sits in the donut.
        Assert.Contains("fm-card-header--stacked-aside", expenses.Find("[data-testid=card-header]").ClassList);
        Assert.Empty(expenses.FindComponents<DistributionViewToggle>());
        Assert.Empty(expenses.FindAll(".fm-card-header-actions"));
        Assert.Empty(expenses.FindAll(".fm-card-header-figure"));
    }

    [Fact]
    public async Task Liabilities_CurrencyToggle_SwitchesBetweenFigureAndChart()
    {
        await using var context = CreateContext();
        var cut = context.Render<LiabilitiesDistributionOverviewCardView>(p => p
            .Add(x => x.TypeData, _single).Add(x => x.AccountData, _two));
        AssertSingleCategoryFigure(cut);

        cut.FindAll("button").Single(x => x.TextContent.Contains("Currency")).Click();
        cut.WaitForAssertion(() =>
        {
            Assert.Single(cut.FindComponents<ApexChart<DistributionSlice>>());
            Assert.Empty(cut.FindAll("[class*=legend--single]"));
        }, TimeSpan.FromSeconds(3));

        cut.FindAll("button").Single(x => x.TextContent.Contains("Type")).Click();
        cut.WaitForAssertion(() =>
        {
            AssertSingleCategoryFigure(cut);
            Assert.Empty(cut.FindComponents<ApexChart<DistributionSlice>>());
        }, TimeSpan.FromSeconds(3));
    }

    private static void AssertSingleCategoryFigure(IRenderedComponent<IComponent> cut)
    {
        var legend = cut.Find("[data-testid=distribution-legend]");
        Assert.Contains("legend--single", legend.ClassName);
        Assert.Matches("100[.,]00", legend.TextContent);
        Assert.Contains("100.0%", legend.TextContent.Replace(",", "."));
        Assert.Empty(cut.FindComponents<ApexChart<NameValueResult>>());
        Assert.Empty(cut.FindComponents<ApexChart<DistributionSlice>>());
    }

    private static void AssertSummary(IRenderedComponent<IComponent> cut)
    {
        Assert.Single(cut.FindAll(".fm-legend-row"));
        Assert.Contains("Mortgage", cut.Find(".fm-legend-name").TextContent);
        Assert.Matches("100[.,]00", cut.Find(".fm-legend-amt").TextContent);
        Assert.StartsWith("100", cut.Find(".fm-legend-pct").TextContent);
    }

    private static void AssertActionsPrecedeFigure(IRenderedComponent<IComponent> cut)
    {
        Assert.Contains("fm-card-header--stacked-aside", cut.Find("[data-testid=card-header]").ClassList);
        var asideItems = cut.FindAll(".fm-card-header-aside > div");
        Assert.Equal(2, asideItems.Count);
        Assert.Contains("fm-card-header-actions", asideItems[0].ClassList);
        Assert.Contains("fm-card-header-figure", asideItems[1].ClassList);
    }

    private static void AssertHeaderHasOnlyToggle(IRenderedComponent<IComponent> cut)
    {
        Assert.Contains("fm-card-header--stacked-aside", cut.Find("[data-testid=card-header]").ClassList);
        var asideItem = Assert.Single(cut.FindAll(".fm-card-header-aside > div"));
        Assert.Contains("fm-card-header-actions", asideItem.ClassList);
    }

    private static string BarColor(AngleSharp.Dom.IElement bar) =>
        bar.GetAttribute("style")!.Split("background:")[1];

    private static void AssertPhoneAutoHeight(IRenderedComponent<IComponent> cut, string height)
    {
        var card = cut.Find(".mud-card");
        Assert.Contains("fm-phone-auto-height", card.ClassList);
        Assert.Contains($"height:{height}", card.GetAttribute("style"));
    }

    private static BunitContext CreateContext()
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddMudServices();
        return context;
    }
}