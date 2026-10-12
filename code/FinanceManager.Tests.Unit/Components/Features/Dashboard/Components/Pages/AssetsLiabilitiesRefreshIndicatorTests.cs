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
    [Fact]
    public async Task AssetsPage_PortfolioCards_SettleWithoutRestartingRequestsOnIndicatorChanges()
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
        await cut.WaitForAssertionAsync(() =>
        {
            Assert.Equal(3, handler.Requests);
            Assert.Equal(2, cut.FindAll(".fm-refreshing-frame[aria-busy='true']").Count);
        });
        handler.Complete();
        await cut.WaitForAssertionAsync(() =>
        {
            Assert.Empty(cut.FindAll(".fm-refreshing-frame[aria-busy='true']"));
            Assert.Contains("20.00%", cut.Markup);
        });

        var history = cut.FindComponent<Stub<AssetsTimeSeriesCardContainer>>();
        await cut.InvokeAsync(() => history.Instance.Parameters.Get(card => card.RefreshingChanged).InvokeAsync(true));
        await cut.InvokeAsync(() => history.Instance.Parameters.Get(card => card.RefreshingChanged).InvokeAsync(false));
        Assert.Equal(3, handler.Requests);
        Assert.Empty(cut.FindAll(".fm-refreshing-frame[aria-busy='true']"));
        Assert.Contains("20.00%", cut.Markup);
    }

    private sealed class PortfolioHandler : HttpMessageHandler
    {
        private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _requests;
        public int Requests => Volatile.Read(ref _requests);
        public void Complete() => _release.SetResult();

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _requests);
            await _release.Task.WaitAsync(cancellationToken);
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

    // A card reporting that its reload is in flight puts the refresh indicator on its own frame only,
    // and the indicator clears when the card reports the reload finished. #912
    [Fact]
    public async Task AssetsPage_CardReportingRefresh_ShowsAndClearsIndicator()
    {
        await using var context = OverviewPagesInitialRangeTests.CreateContext();
        var cut = context.Render<AssetsPage>();
        var card = cut.FindComponent<Stub<AssetsTimeSeriesCardContainer>>();
        Assert.Empty(cut.FindAll(".mud-progress-linear"));

        var callback = card.Instance.Parameters.Get(x => x.RefreshingChanged);
        await cut.InvokeAsync(() => card.Instance.Parameters.Get(x => x.RefreshingChanged).InvokeAsync(true));
        Assert.Single(cut.FindAll(".mud-progress-linear"));
        Assert.Equal(callback, card.Instance.Parameters.Get(x => x.RefreshingChanged));

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

        var callback = card.Instance.Parameters.Get(x => x.RefreshingChanged);
        await cut.InvokeAsync(() => card.Instance.Parameters.Get(x => x.RefreshingChanged).InvokeAsync(true));
        Assert.Single(cut.FindAll(".mud-progress-linear"));
        Assert.Equal(callback, card.Instance.Parameters.Get(x => x.RefreshingChanged));

        await cut.InvokeAsync(() => card.Instance.Parameters.Get(x => x.RefreshingChanged).InvokeAsync(false));
        Assert.Empty(cut.FindAll(".mud-progress-linear"));
    }
}