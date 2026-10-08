using Bunit;
using Bunit.TestDoubles;
using FinanceManager.Components.Features.Dashboard.Components;
using FinanceManager.Components.Features.Dashboard.Components.Cards;
using FinanceManager.Components.Features.Dashboard.Components.Cards.Assets;
using FinanceManager.Components.Features.Dashboard.Components.Cards.Liabilities;
using FinanceManager.Components.Features.Dashboard.Components.Cards.TimeSeries;
using FinanceManager.Components.Features.Dashboard.HttpClients;
using FinanceManager.Components.Features.Dashboard.Models;
using FinanceManager.Components.Features.Dashboard.Services;
using FinanceManager.Components.Features.FinancialAccounts.Services;
using FinanceManager.Components.Shared.Services;
using FinanceManager.Domain.Dashboard.Dtos;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.Identity.Entities;
using FinanceManager.Domain.Identity.Services;
using FinanceManager.Domain.MoneyFlow.Entities;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MudBlazor.Services;
using System.Net;
using System.Net.Http.Json;
using DashboardComponent = FinanceManager.Components.Features.Dashboard.Components.Dashboard;

namespace FinanceManager.Tests.Unit.Components.Features.Dashboard.Components;

[Trait("Category", "Unit")]
public class DashboardRefreshIndicatorTests
{
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(5);

    private static readonly string[] _rangeDependentCards =
    [
        DashboardCards.NetWorth, DashboardCards.NetCashFlow, DashboardCards.ClosingBalance, DashboardCards.Labels,
        DashboardCards.Assets, DashboardCards.Liabilities, DashboardCards.Expenses,
    ];

    private static readonly string[] _rangeIndependentCards =
    [
        DashboardCards.CashFlowForecast, DashboardCards.Insights, DashboardCards.FinancialAlerts,
        DashboardCards.RecurringTransactions, DashboardCards.TransactionLog,
    ];

    [Fact]
    public void InitialLoad_ShowsNoRefreshIndicator()
    {
        var handler = new OverviewHandler();
        using var context = CreateContext(handler);

        var cut = context.Render<DashboardComponent>();

        Assert.Empty(cut.FindAll(".mud-progress-linear"));
    }

    [Fact]
    public async Task RangeChange_ShowsIndicatorOnRangeDependentCardsOnly_AndClearsWhenFreshDataArrives()
    {
        var handler = new OverviewHandler();
        using var context = CreateContext(handler);
        var cut = await RenderLoaded(context, handler);
        Assert.Empty(cut.FindAll(".mud-progress-linear"));

        var (start, end) = NewRange();
        await ChangeRange(cut, start, end);

        cut.WaitForAssertion(() => Assert.Equal(_rangeDependentCards.Length, cut.FindAll(".mud-progress-linear").Count), _timeout);
        foreach (var cardId in _rangeDependentCards)
            Assert.True(FrameOf(cut, cardId).IsRefreshing, $"{cardId} should show the indicator");
        foreach (var cardId in _rangeIndependentCards)
            Assert.False(FrameOf(cut, cardId).IsRefreshing, $"{cardId} should not show the indicator");
        // The cards keep the period of the data they actually display until the new data arrives.
        Assert.NotEqual(start, DisplayedStart(cut));

        handler.Complete(1, Overview(start, end));

        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(".mud-progress-linear")), _timeout);
        Assert.Equal(start, DisplayedStart(cut));
        Assert.Empty(cut.FindAll(".mud-alert"));
    }

    [Fact]
    public async Task SupersededResponse_DoesNotClearIndicatorOfTheLatestRequest()
    {
        var handler = new OverviewHandler();
        using var context = CreateContext(handler);
        var cut = await RenderLoaded(context, handler);

        var (firstStart, firstEnd) = NewRange(10);
        await ChangeRange(cut, firstStart, firstEnd);
        cut.WaitForAssertion(() => Assert.Equal(2, handler.Count), _timeout);

        var (latestStart, latestEnd) = NewRange(20);
        await ChangeRange(cut, latestStart, latestEnd);
        cut.WaitForAssertion(() => Assert.Equal(3, handler.Count), _timeout);

        // The older request answers last-but-first: it must neither clear the indicator nor repaint.
        var originalStart = DisplayedStart(cut);
        handler.Complete(1, Overview(firstStart, firstEnd));
        await cut.InvokeAsync(async () => await Task.Delay(50));

        Assert.Equal(_rangeDependentCards.Length, cut.FindAll(".mud-progress-linear").Count);
        Assert.Equal(originalStart, DisplayedStart(cut));

        handler.Complete(2, Overview(latestStart, latestEnd));

        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(".mud-progress-linear")), _timeout);
        Assert.Equal(latestStart, DisplayedStart(cut));
    }

    [Fact]
    public async Task FailedRefresh_KeepsDataAndPeriod_ShowsAlertWithRetry_AndRetryRecovers()
    {
        var handler = new OverviewHandler();
        using var context = CreateContext(handler);
        var cut = await RenderLoaded(context, handler);
        var originalStart = DisplayedStart(cut);

        var (start, end) = NewRange();
        await ChangeRange(cut, start, end);
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".mud-progress-linear")), _timeout);

        handler.Fail(1);

        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(".mud-progress-linear")), _timeout);
        Assert.Contains("Unable to load dashboard data", cut.Find(".mud-alert").TextContent);
        Assert.Equal(originalStart, DisplayedStart(cut));
        Assert.NotEmpty(cut.FindComponents<Stub<NetWorthTimeSeriesCard>>());

        cut.FindAll("button").Single(button => button.TextContent.Trim() == "Retry").Click();

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".mud-progress-linear")), _timeout);
        Assert.Empty(cut.FindAll(".mud-alert"));

        handler.Complete(2, Overview(start, end));

        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(".mud-progress-linear")), _timeout);
        Assert.Equal(start, DisplayedStart(cut));
        Assert.Empty(cut.FindAll(".mud-alert"));
    }

    private static (DateTime Start, DateTime End) NewRange(int days = 10)
    {
        var end = DateTime.UtcNow.Date.AddDays(-days);
        return (end.AddDays(-7), end);
    }

    private static async Task<IRenderedComponent<DashboardComponent>> RenderLoaded(BunitContext context, OverviewHandler handler)
    {
        var cut = context.Render<DashboardComponent>();
        cut.WaitForAssertion(() => Assert.Equal(1, handler.Count), _timeout);
        handler.Complete(0, Overview(cut.Instance.StartDate, cut.Instance.EndDate));
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindComponents<Stub<NetWorthTimeSeriesCard>>()), _timeout);
        await cut.InvokeAsync(async () => await Task.Delay(50));
        return cut;
    }

    private static Task ChangeRange(IRenderedComponent<DashboardComponent> cut, DateTime start, DateTime end) =>
        cut.InvokeAsync(() => cut.Instance.DateChanged((start, end)));

    private static DateTime DisplayedStart(IRenderedComponent<DashboardComponent> cut) =>
        cut.FindComponent<Stub<NetWorthTimeSeriesCard>>().Instance.Parameters.Get(x => x.StartDateTime);

    // Each card sits in its own frame; the stub's marker attribute says which card that is.
    private static RefreshingCardFrame FrameOf(IRenderedComponent<DashboardComponent> cut, string cardId) =>
        cut.FindComponents<RefreshingCardFrame>()
            .Single(frame => frame.FindAll($"[data-card='{cardId}']").Count > 0).Instance;

    private static DashboardOverviewDto Overview(DateTime start, DateTime end)
    {
        List<NameValueResult> values = [new() { Name = "Item", Value = 10 }];
        return new DashboardOverviewDto
        {
            UserId = 1,
            CurrencyId = DefaultCurrency.PLN.Id,
            StartDate = start,
            EndDate = end,
            NetWorthSeries = [new(start, 10)],
            NetCashFlowSeries = [new(start, 5)],
            ClosingBalanceSeries = [new(start, 7)],
            LiabilitiesPerType = values,
            LiabilitiesPerAccount = values,
            LabelsValue = values,
            AssetsPerType = values,
            AssetsPerAccount = values,
            ExpenseDistribution = values,
        };
    }

    private static BunitContext CreateContext(OverviewHandler handler)
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddLogging();
        context.Services.AddMudServices();

        AddCardStub<NetWorthTimeSeriesCard>(context, DashboardCards.NetWorth);
        AddCardStub<NetCashFlowOverviewCard>(context, DashboardCards.NetCashFlow);
        AddCardStub<CashFlowForecastCard>(context, DashboardCards.CashFlowForecast);
        AddCardStub<ClosingBalanceOverviewCard>(context, DashboardCards.ClosingBalance);
        AddCardStub<FinancialLabelsListCard>(context, DashboardCards.Labels);
        AddCardStub<AssetsDistributionOverviewCard>(context, DashboardCards.Assets);
        AddCardStub<LiabilitiesDistributionOverviewCard>(context, DashboardCards.Liabilities);
        AddCardStub<ExpenseDistributionOverviewCard>(context, DashboardCards.Expenses);
        AddCardStub<FinancialInsightsCarousel>(context, DashboardCards.Insights);
        AddCardStub<FinancialAlertsCard>(context, DashboardCards.FinancialAlerts);
        AddCardStub<RecurringTransactionDetectorCard>(context, DashboardCards.RecurringTransactions);
        AddCardStub<TransactionLogCard>(context, DashboardCards.TransactionLog);
        context.ComponentFactories.AddStub<DashboardDatePicker>();
        context.ComponentFactories.AddStub<DashboardRowFiller>();

        var login = new Mock<ILoginService>();
        login.Setup(x => x.GetLoggedUser()).ReturnsAsync(new UserSession { UserId = 1, UserName = "tester", Password = "", UserRole = UserRole.User });
        var settings = new Mock<ISettingsService>();
        settings.Setup(x => x.GetCurrencyAsync()).ReturnsAsync(DefaultCurrency.PLN);
        var snapshots = new Mock<ISnapshotService>();

        context.Services.AddSingleton(login.Object);
        context.Services.AddSingleton(settings.Object);
        context.Services.AddSingleton(snapshots.Object);
        context.Services.AddSingleton(Mock.Of<IFinancialAccountService>());
        context.Services.AddSingleton<ISnapshotRefreshCoordinator>(
            new SnapshotRefreshCoordinator(snapshots.Object, NullLogger<SnapshotRefreshCoordinator>.Instance));
        context.Services.AddSingleton(new DashboardCardVisibilityService(
            snapshots.Object, login.Object, NullLogger<DashboardCardVisibilityService>.Instance));
        context.Services.AddSingleton(new DashboardHttpClient(
            new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") }));
        return context;
    }

    private static void AddCardStub<TCard>(BunitContext context, string cardId) where TCard : IComponent =>
        context.ComponentFactories.AddStub<TCard>($"<span data-card=\"{cardId}\"></span>");

    // Every overview request waits on a test-controlled response, in the order requests arrive.
    private sealed class OverviewHandler : HttpMessageHandler
    {
        private readonly List<TaskCompletionSource<HttpResponseMessage>> _responses = [];

        public int Count
        {
            get { lock (_responses) return _responses.Count; }
        }

        public void Complete(int index, DashboardOverviewDto overview) =>
            Response(index).SetResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(overview) });

        public void Fail(int index) =>
            Response(index).SetResult(new HttpResponseMessage(HttpStatusCode.InternalServerError));

        private TaskCompletionSource<HttpResponseMessage> Response(int index)
        {
            lock (_responses) return _responses[index];
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
            lock (_responses) _responses.Add(response);
            return response.Task;
        }
    }
}