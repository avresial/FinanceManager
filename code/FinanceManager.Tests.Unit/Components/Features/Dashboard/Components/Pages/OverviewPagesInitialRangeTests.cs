using Bunit;
using FinanceManager.Components.Features.Dashboard.Components;
using FinanceManager.Components.Features.Dashboard.Components.Cards;
using FinanceManager.Components.Features.Dashboard.Components.Cards.Assets;
using FinanceManager.Components.Features.Dashboard.Components.Cards.Liabilities;
using FinanceManager.Components.Features.Dashboard.Components.Cards.TimeSeries;
using FinanceManager.Components.Features.Dashboard.Components.Pages;

namespace FinanceManager.Tests.Unit.Components.Features.Dashboard.Components.Pages;

[Trait("Category", "Unit")]
public class OverviewPagesInitialRangeTests
{
    // The Assets and Liabilities pages must open on the window the Dashboard seeds — a rolling
    // 31 days anchored to midnight — instead of the current calendar month, which is nearly
    // empty at the start of a month. #700
    [Fact]
    public void AssetsPage_InitializesWithDefaultOverviewRange()
    {
        using var context = CreateContext();
        var before = DateTime.UtcNow;
        var cut = context.Render<AssetsPage>();
        var after = DateTime.UtcNow;

        AssertDefaultOverviewRange(before, after, () => cut.Instance.StartDate, () => cut.Instance.EndDate);
        Assert.Contains("mud-container", cut.Markup);
    }

    [Fact]
    public void LiabilitiesPage_InitializesWithDefaultOverviewRange()
    {
        using var context = CreateContext();
        var before = DateTime.UtcNow;
        var cut = context.Render<LiabilitiesPage>();
        var after = DateTime.UtcNow;

        AssertDefaultOverviewRange(before, after, () => cut.Instance.StartDate, () => cut.Instance.EndDate);
    }

    private static void AssertDefaultOverviewRange(DateTime before, DateTime after, Func<DateTime> startDate, Func<DateTime> endDate)
    {
        // Asserted as the relationship the shared helper defines rather than against a captured
        // "now", so the expectation holds even if the clock crosses midnight mid-test.
        Assert.InRange(endDate(), before, after);
        Assert.Equal(endDate().Date.AddDays(-30), startDate());
        Assert.Equal(DateTimeKind.Utc, startDate().Kind);
    }

    private static BunitContext CreateContext()
    {
        var context = new BunitContext();
        context.ComponentFactories.AddStub<AssetsTimeSeriesCard>();
        context.ComponentFactories.AddStub<AssetsDistributionOverviewCard>();
        context.ComponentFactories.AddStub<InvestmentPaycheckEstimatorCard>();
        context.ComponentFactories.AddStub<InvestmentRateCard>();
        context.ComponentFactories.AddStub<DiversificationProxyCard>();
        context.ComponentFactories.AddStub<LiabilitiesTimeSeriesCard>();
        context.ComponentFactories.AddStub<LiabilitiesDistributionOverviewCard>();
        context.ComponentFactories.AddStub<DashboardDatePicker>();
        return context;
    }
}