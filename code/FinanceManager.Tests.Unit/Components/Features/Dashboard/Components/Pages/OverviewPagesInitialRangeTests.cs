using Blazored.LocalStorage;
using Bunit;
using Bunit.TestDoubles;
using FinanceManager.Components.Features.Dashboard.Components;
using FinanceManager.Components.Features.Dashboard.Components.Cards;
using FinanceManager.Components.Features.Dashboard.Components.Cards.Assets;
using FinanceManager.Components.Features.Dashboard.Components.Cards.Liabilities;
using FinanceManager.Components.Features.Dashboard.Components.Cards.TimeSeries;
using FinanceManager.Components.Features.Dashboard.Components.Pages;
using FinanceManager.Components.Features.Dashboard.Services;
using FinanceManager.Components.Features.MoneyFlow.HttpClients;
using FinanceManager.Components.Shared.Services;
using FinanceManager.Domain.Identity.Services;
using Microsoft.AspNetCore.Components.Sections;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MudBlazor.Services;

namespace FinanceManager.Tests.Unit.Components.Features.Dashboard.Components.Pages;

[Trait("Category", "Unit")]
public class OverviewPagesInitialRangeTests
{
    // The Assets and Liabilities pages must open on the window the Dashboard seeds — a rolling
    // 31 days anchored to midnight — instead of the current calendar month, which is nearly
    // empty at the start of a month. #700
    [Fact]
    public async Task AssetsPage_InitializesWithDefaultOverviewRange()
    {
        await using var context = CreateContext();
        var before = DateTime.UtcNow;
        var actions = context.Render<SectionOutlet>(parameters => parameters.Add(section => section.SectionName, "page-actions"));
        var cut = context.Render<AssetsPage>();
        Assert.NotNull(actions.FindComponent<Stub<DashboardDatePicker>>());
        Assert.Empty(cut.FindAll(".fm-page-header"));
        var after = DateTime.UtcNow;

        AssertDefaultOverviewRange(before, after, () => cut.Instance.StartDate, () => cut.Instance.EndDate);
        Assert.Contains("mud-container", cut.Markup);
    }

    [Fact]
    public async Task LiabilitiesPage_InitializesWithDefaultOverviewRange()
    {
        await using var context = CreateContext();
        var before = DateTime.UtcNow;
        var actions = context.Render<SectionOutlet>(parameters => parameters.Add(section => section.SectionName, "page-actions"));
        var cut = context.Render<LiabilitiesPage>();
        Assert.NotNull(actions.FindComponent<Stub<DashboardDatePicker>>());
        Assert.Empty(cut.FindAll(".fm-page-header"));
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

    internal static BunitContext CreateContext()
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddMudServices();
        context.Services.AddSingleton(new DashboardCardVisibilityService(Mock.Of<ISnapshotService>(),
            Mock.Of<ILoginService>(), NullLogger<DashboardCardVisibilityService>.Instance));
        context.Services.AddSingleton(new AssetsPageCardsCacheService(Mock.Of<ILocalStorageService>(),
            new MemoryCache(new MemoryCacheOptions()), new AssetsHttpClient(new HttpClient()),
            NullLogger<AssetsPageCardsCacheService>.Instance));
        context.ComponentFactories.AddStub<AssetsTimeSeriesCardContainer>();
        context.ComponentFactories.AddStub<AssetsDistributionOverviewCard>();
        context.ComponentFactories.AddStub<PortfolioReturnCardContainer>();
        context.ComponentFactories.AddStub<PortfolioReturnAttributionCardContainer>();
        context.ComponentFactories.AddStub<InvestmentPaycheckEstimatorCard>();
        context.ComponentFactories.AddStub<InvestmentRateCard>();
        context.ComponentFactories.AddStub<DiversificationProxyCard>();
        context.ComponentFactories.AddStub<FeeDragCard>();
        context.ComponentFactories.AddStub<LiabilitiesTimeSeriesCard>();
        context.ComponentFactories.AddStub<LiabilitiesDistributionOverviewCard>();
        context.ComponentFactories.AddStub<DashboardDatePicker>();
        return context;
    }
}