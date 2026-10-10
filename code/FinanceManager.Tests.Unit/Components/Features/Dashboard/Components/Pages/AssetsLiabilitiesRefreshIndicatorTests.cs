using Bunit;
using Bunit.TestDoubles;
using FinanceManager.Components.Features.Dashboard.Components.Cards.Assets;
using FinanceManager.Components.Features.Dashboard.Components.Cards.Liabilities;
using FinanceManager.Components.Features.Dashboard.Components.Cards.TimeSeries;
using FinanceManager.Components.Features.Dashboard.Components.Pages;

namespace FinanceManager.Tests.Unit.Components.Features.Dashboard.Components.Pages;

[Trait("Category", "Unit")]
public class AssetsLiabilitiesRefreshIndicatorTests
{
    // A card reporting that its reload is in flight puts the refresh indicator on its own frame only,
    // and the indicator clears when the card reports the reload finished. #912
    [Fact]
    public async Task AssetsPage_CardReportingRefresh_ShowsAndClearsIndicator()
    {
        await using var context = OverviewPagesInitialRangeTests.CreateContext();
        var cut = context.Render<AssetsPage>();
        var card = cut.FindComponent<Stub<AssetsTimeSeriesCardContainer>>();
        Assert.Empty(cut.FindAll(".mud-progress-linear"));

        await cut.InvokeAsync(() => card.Instance.Parameters.Get(x => x.RefreshingChanged).InvokeAsync(true));
        Assert.Single(cut.FindAll(".mud-progress-linear"));

        await cut.InvokeAsync(() => card.Instance.Parameters.Get(x => x.RefreshingChanged).InvokeAsync(false));
        Assert.Empty(cut.FindAll(".mud-progress-linear"));
    }

    [Fact]
    public async Task LiabilitiesPage_CardReportingRefresh_ShowsAndClearsIndicator()
    {
        await using var context = OverviewPagesInitialRangeTests.CreateContext();
        var cut = context.Render<LiabilitiesPage>();
        var card = cut.FindComponent<Stub<LiabilitiesTimeSeriesCard>>();
        Assert.Empty(cut.FindAll(".mud-progress-linear"));

        await cut.InvokeAsync(() => card.Instance.Parameters.Get(x => x.RefreshingChanged).InvokeAsync(true));
        Assert.Single(cut.FindAll(".mud-progress-linear"));

        await cut.InvokeAsync(() => card.Instance.Parameters.Get(x => x.RefreshingChanged).InvokeAsync(false));
        Assert.Empty(cut.FindAll(".mud-progress-linear"));
    }
}