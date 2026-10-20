using Bunit;
using Bunit.TestDoubles;
using FinanceManager.Components.Features.Dashboard.Components.Cards.Assets;
using FinanceManager.Components.Features.Dashboard.Components.Cards.Liabilities;
using FinanceManager.Components.Features.Dashboard.Components.Cards.TimeSeries;
using FinanceManager.Components.Features.Dashboard.Components.Pages;
using FinanceManager.Components.Shared.Services;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.Identity.Entities;
using FinanceManager.Domain.Identity.Services;
using FinanceManager.Domain.MoneyFlow.Entities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Net;
using System.Net.Http.Json;

namespace FinanceManager.Tests.Unit.Components.Features.Dashboard.Components.Pages;

[Trait("Category", "Unit")]
public class AssetsLiabilitiesRefreshIndicatorTests
{
    // Opening the page revalidates silently; only a range the user picks shows the indicator. #985
    [Fact]
    public async Task AssetsPage_PortfolioCards_ShowIndicatorOnlyForUserRangeChange()
    {
        using var handler = new PortfolioHandler();
        await using var context = OverviewPagesInitialRangeTests.CreateContext(handler);
        context.Services.AddLogging();
        var login = new Mock<ILoginService>();
        login.Setup(service => service.GetLoggedUser()).ReturnsAsync(new UserSession { UserId = 1, UserName = "tester", Password = string.Empty, UserRole = UserRole.User });
        var settings = new Mock<ISettingsService>();
        settings.Setup(service => service.GetCurrencyAsync()).ReturnsAsync(DefaultCurrency.PLN);
        context.Services.AddSingleton(login.Object);
        context.Services.AddSingleton(settings.Object);
        context.Services.AddSingleton<ISnapshotRefreshCoordinator>(new SnapshotRefreshCoordinator(
            Mock.Of<ISnapshotService>(), NullLogger<SnapshotRefreshCoordinator>.Instance));
        var cut = context.Render<AssetsPage>();
        await cut.WaitForAssertionAsync(() => Assert.Equal(3, handler.Requests));
        Assert.Empty(cut.FindAll(".fm-refreshing-frame[aria-busy='true']"));

        handler.Complete();
        await cut.WaitForAssertionAsync(() => Assert.Contains("20.00%", cut.Markup));
        Assert.Empty(cut.FindAll(".fm-refreshing-frame[aria-busy='true']"));

        handler.Block();
        await cut.InvokeAsync(() => cut.Instance.DateChanged((DateTime.UtcNow.Date.AddDays(-90), DateTime.UtcNow)));
        await cut.WaitForAssertionAsync(() =>
        {
            Assert.Equal(6, handler.Requests);
            Assert.Equal(2, cut.FindAll(".fm-refreshing-frame[aria-busy='true']").Count);
        });

        handler.Complete();
        await cut.WaitForAssertionAsync(() => Assert.Empty(cut.FindAll(".fm-refreshing-frame[aria-busy='true']")));
        Assert.Equal(6, handler.Requests);
    }

    private sealed class PortfolioHandler : HttpMessageHandler
    {
        private TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _requests;
        public int Requests => Volatile.Read(ref _requests);
        public void Complete() => Volatile.Read(ref _release).TrySetResult();
        public void Block() => Volatile.Write(ref _release, new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously));

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _requests);
            await Volatile.Read(ref _release).Task.WaitAsync(cancellationToken);
            var start = DateTime.UtcNow.Date.AddDays(-30);
            var end = DateTime.UtcNow;
            object model = request.RequestUri!.AbsolutePath.Contains("GetMoneyWeightedReturn", StringComparison.Ordinal)
                ? new MoneyWeightedReturnResult(0.2m, MoneyWeightedReturnStatus.Available, start, end)
                : request.RequestUri.AbsolutePath.Contains("GetTimeWeightedReturn", StringComparison.Ordinal)
                    ? new TimeWeightedReturnResult(0.1m, TimeWeightedReturnStatus.Available, start, end)
                    : PortfolioReturnAttributionResult.Available(100m, 0m, 100m, 0m, 0m, start, end);
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(model) };
        }
    }

    // A card reporting that its reload for a user-picked range is in flight puts the refresh indicator
    // on its own frame only, and the indicator clears when the card reports the reload finished. #912
    [Fact]
    public async Task AssetsPage_RangeChange_ShowsIndicatorUntilCardReportsFinished()
    {
        await using var context = OverviewPagesInitialRangeTests.CreateContext();
        var cut = context.Render<AssetsPage>();
        var card = cut.FindComponent<Stub<AssetsTimeSeriesCardContainer>>();
        var callback = card.Instance.Parameters.Get(x => x.RefreshingChanged);

        await cut.InvokeAsync(() => cut.Instance.DateChanged((DateTime.UtcNow.Date.AddDays(-90), DateTime.UtcNow)));
        await cut.InvokeAsync(() => card.Instance.Parameters.Get(x => x.RefreshingChanged).InvokeAsync(true));
        Assert.Single(cut.FindAll(".mud-progress-linear"));
        Assert.Equal(callback, card.Instance.Parameters.Get(x => x.RefreshingChanged));

        await cut.InvokeAsync(() => card.Instance.Parameters.Get(x => x.RefreshingChanged).InvokeAsync(false));
        Assert.Empty(cut.FindAll(".mud-progress-linear"));
    }

    // Initial navigation and background revalidation stay silent; a cache miss shows the card's own
    // loading state instead of the frame indicator. #985
    [Fact]
    public async Task AssetsPage_ReloadWithoutUserRangeChange_DoesNotShowIndicator()
    {
        await using var context = OverviewPagesInitialRangeTests.CreateContext();
        var cut = context.Render<AssetsPage>();
        var card = cut.FindComponent<Stub<AssetsTimeSeriesCardContainer>>();

        await cut.InvokeAsync(() => card.Instance.Parameters.Get(x => x.RefreshingChanged).InvokeAsync(true));
        Assert.Empty(cut.FindAll(".mud-progress-linear"));
        await cut.InvokeAsync(() => card.Instance.Parameters.Get(x => x.RefreshingChanged).InvokeAsync(false));

        // Once the range change has settled, a later background reload is silent again.
        await cut.InvokeAsync(() => cut.Instance.DateChanged((DateTime.UtcNow.Date.AddDays(-90), DateTime.UtcNow)));
        await cut.InvokeAsync(() => card.Instance.Parameters.Get(x => x.RefreshingChanged).InvokeAsync(true));
        await cut.InvokeAsync(() => card.Instance.Parameters.Get(x => x.RefreshingChanged).InvokeAsync(false));
        await cut.InvokeAsync(() => card.Instance.Parameters.Get(x => x.RefreshingChanged).InvokeAsync(true));
        Assert.Empty(cut.FindAll(".mud-progress-linear"));
    }

    // Each card's indicator follows its own reload: one card finishing (or failing) does not clear
    // another's, and quick successive range changes keep the indicator until the latest reload ends.
    [Fact]
    public async Task AssetsPage_RangeChange_IndicatorsSettlePerCard()
    {
        await using var context = OverviewPagesInitialRangeTests.CreateContext();
        var cut = context.Render<AssetsPage>();
        var history = cut.FindComponent<Stub<AssetsTimeSeriesCardContainer>>();
        var distribution = cut.FindComponent<Stub<AssetsDistributionOverviewCard>>();

        await cut.InvokeAsync(() => cut.Instance.DateChanged((DateTime.UtcNow.Date.AddDays(-90), DateTime.UtcNow)));
        await cut.InvokeAsync(() => history.Instance.Parameters.Get(x => x.RefreshingChanged).InvokeAsync(true));
        await cut.InvokeAsync(() => distribution.Instance.Parameters.Get(x => x.RefreshingChanged).InvokeAsync(true));
        await cut.InvokeAsync(() => cut.Instance.DateChanged((DateTime.UtcNow.Date.AddDays(-30), DateTime.UtcNow)));
        await cut.InvokeAsync(() => history.Instance.Parameters.Get(x => x.RefreshingChanged).InvokeAsync(true));
        Assert.Equal(2, cut.FindAll(".mud-progress-linear").Count);

        await cut.InvokeAsync(() => history.Instance.Parameters.Get(x => x.RefreshingChanged).InvokeAsync(false));
        Assert.Single(cut.FindAll(".mud-progress-linear"));

        await cut.InvokeAsync(() => distribution.Instance.Parameters.Get(x => x.RefreshingChanged).InvokeAsync(false));
        Assert.Empty(cut.FindAll(".mud-progress-linear"));
    }

    [Fact]
    public async Task LiabilitiesPage_CardReportingRefresh_ShowsAndClearsIndicator()
    {
        await using var context = OverviewPagesInitialRangeTests.CreateContext();
        var cut = context.Render<LiabilitiesPage>();
        var card = cut.FindComponent<Stub<LiabilitiesTimeSeriesCard>>();
        Assert.Empty(cut.FindAll(".mud-progress-linear"));

        var callback = card.Instance.Parameters.Get(x => x.RefreshingChanged);
        await cut.InvokeAsync(() => card.Instance.Parameters.Get(x => x.RefreshingChanged).InvokeAsync(true));
        Assert.Single(cut.FindAll(".mud-progress-linear"));
        Assert.Equal(callback, card.Instance.Parameters.Get(x => x.RefreshingChanged));

        await cut.InvokeAsync(() => card.Instance.Parameters.Get(x => x.RefreshingChanged).InvokeAsync(false));
        Assert.Empty(cut.FindAll(".mud-progress-linear"));
    }
}