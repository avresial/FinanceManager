using Bunit;
using FinanceManager.Components.Features.Dashboard.Components.Cards.Assets;
using FinanceManager.Components.Features.Dashboard.Components.Cards.Liabilities;
using FinanceManager.Components.Features.Dashboard.Components.Cards.TimeSeries;
using FinanceManager.Components.Features.Dashboard.Services;
using FinanceManager.Components.Features.MoneyFlow.HttpClients;
using FinanceManager.Components.Shared.Services;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.Identity.Entities;
using FinanceManager.Domain.Identity.Services;
using FinanceManager.Domain.MoneyFlow.Entities;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MudBlazor.Services;
using System.Net;
using System.Net.Http.Json;

namespace FinanceManager.Tests.Unit.Components.Features.Dashboard.Components.Cards;

[Trait("Category", "Unit")]
public class RangeRefreshFailureTests
{
    private static readonly DateTime _start = new(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime _end = _start.AddDays(6);

    [Theory]
    [InlineData(typeof(AssetsDistributionOverviewCard), "[data-testid=distribution-legend]")]
    [InlineData(typeof(AssetsTimeSeriesCardContainer), ".fm-tsvc-value")]
    [InlineData(typeof(PortfolioReturnCardContainer), ".portfolio-return-value--money")]
    [InlineData(typeof(PortfolioReturnAttributionCardContainer), "[data-testid=return-attribution-total]")]
    [InlineData(typeof(LiabilitiesDistributionOverviewCard), "[data-testid=distribution-legend]")]
    [InlineData(typeof(LiabilitiesTimeSeriesCard), ".fm-tsvc-value")]
    public async Task FailedNewRange_KeepsDisplayedDataAndRetryReplacesIt(Type card, string valueSelector)
    {
        var handler = new RangeHandler();
        await using var context = Context(handler);
        var refreshing = new List<bool>();
        var cut = context.Render<RangeHost>(p => p
            .Add(x => x.Card, card).Add(x => x.Start, _start).Add(x => x.End, _end)
            .Add(x => x.Refreshing, refreshing.Add));
        cut.WaitForAssertion(() => Assert.False(refreshing.Last()));
        var previousValue = cut.Find(valueSelector).TextContent;
        var rangeSelector = card == typeof(PortfolioReturnCardContainer) ? ".fm-card-header-subtitle"
            : card == typeof(PortfolioReturnAttributionCardContainer) ? ".fm-card-header-subtitle" : null;
        var previousRange = rangeSelector is null ? null : cut.Find(rangeSelector).TextContent;
        handler.Phase = 1;
        cut.Render(p => p.Add(x => x.Start, _start.AddDays(7)).Add(x => x.End, _end.AddDays(7)));
        Assert.True(refreshing.Last());
        Assert.Equal(previousValue, cut.Find(valueSelector).TextContent);
        if (rangeSelector is not null) Assert.Equal(previousRange, cut.Find(rangeSelector).TextContent);
        handler.FailRange();
        cut.WaitForAssertion(() =>
        {
            Assert.False(refreshing.Last());
            Assert.Equal(previousValue, cut.Find(valueSelector).TextContent);
            Assert.Single(cut.FindAll(".mud-alert"));
        });
        handler.Phase = 2;
        await cut.InvokeAsync(() => cut.FindAll("button").Single(x => x.TextContent.Contains("Retry", StringComparison.Ordinal)).Click());
        cut.WaitForAssertion(() =>
        {
            Assert.False(refreshing.Last());
            Assert.NotEqual(previousValue, cut.Find(valueSelector).TextContent);
            Assert.Empty(cut.FindAll(".mud-alert"));
            if (rangeSelector is not null) Assert.NotEqual(previousRange, cut.Find(rangeSelector).TextContent);
        });
    }

    [Theory]
    [InlineData(typeof(AssetsDistributionOverviewCard), "[data-testid=distribution-legend]")]
    [InlineData(typeof(AssetsTimeSeriesCardContainer), ".fm-tsvc-value")]
    [InlineData(typeof(PortfolioReturnCardContainer), ".portfolio-return-value--money")]
    [InlineData(typeof(PortfolioReturnAttributionCardContainer), "[data-testid=return-attribution-total]")]
    [InlineData(typeof(LiabilitiesDistributionOverviewCard), "[data-testid=distribution-legend]")]
    [InlineData(typeof(LiabilitiesTimeSeriesCard), ".fm-tsvc-value")]
    public async Task ChangedOwner_DoesNotRetainPreviousUsersData(Type card, string valueSelector)
    {
        var handler = new RangeHandler();
        await using var context = Context(handler);
        var refreshing = new List<bool>();
        var cut = context.Render<RangeHost>(p => p
            .Add(x => x.Card, card).Add(x => x.Start, _start).Add(x => x.End, _end)
            .Add(x => x.Refreshing, refreshing.Add));
        cut.WaitForAssertion(() => Assert.False(refreshing.Last()));
        var previousValue = cut.Find(valueSelector).TextContent;
        Mock.Get(context.Services.GetRequiredService<ILoginService>()).Setup(x => x.GetLoggedUser())
            .ReturnsAsync(new UserSession { UserId = 2, UserName = "other", Password = "", UserRole = UserRole.User });
        handler.Phase = 1;
        cut.Render(p => p.Add(x => x.Start, _start.AddDays(7)));
        Assert.DoesNotContain(cut.FindAll(valueSelector), x => x.TextContent == previousValue);
        handler.FailRange();
        cut.WaitForAssertion(() =>
        {
            Assert.False(refreshing.Last());
            Assert.DoesNotContain(cut.FindAll(valueSelector), x => x.TextContent == previousValue);
        });
    }

    private static BunitContext Context(RangeHandler handler)
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddLogging();
        context.Services.AddMudServices();
        var login = new Mock<ILoginService>();
        login.Setup(x => x.GetLoggedUser()).ReturnsAsync(new UserSession
        { UserId = 1, UserName = "tester", Password = "", UserRole = UserRole.User });
        var settings = new Mock<ISettingsService>();
        settings.Setup(x => x.GetCurrencyAsync()).ReturnsAsync(DefaultCurrency.PLN);
        context.Services.AddSingleton(login.Object);
        context.Services.AddSingleton(settings.Object);
        var coordinator = new SnapshotRefreshCoordinator(Mock.Of<ISnapshotService>(), NullLogger<SnapshotRefreshCoordinator>.Instance);
        context.Services.AddSingleton<ISnapshotRefreshCoordinator>(coordinator);
        context.Services.AddSingleton(new LiabilitiesSnapshotStore(coordinator));
        var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") };
        context.Services.AddSingleton(new LiabilitiesHttpClient(http));
        context.Services.AddSingleton(new AssetsPageCardsRequestCoordinator(new AssetsHttpClient(http), NullLogger<AssetsPageCardsRequestCoordinator>.Instance));
        return context;
    }

    public sealed class RangeHost : ComponentBase
    {
        [Parameter] public Type Card { get; set; } = typeof(AssetsDistributionOverviewCard);
        [Parameter] public DateTime Start { get; set; }
        [Parameter] public DateTime End { get; set; }
        [Parameter] public Action<bool> Refreshing { get; set; } = _ => { };
        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            builder.OpenComponent(0, Card);
            builder.AddAttribute(1, "StartDateTime", Start);
            builder.AddAttribute(2, "EndDateTime", End);
            builder.AddAttribute(3, "RefreshingChanged", EventCallback.Factory.Create(new object(), Refreshing));
            builder.CloseComponent();
        }
    }

    private sealed class RangeHandler : HttpMessageHandler
    {
        private readonly TaskCompletionSource _failure = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int Phase { get; set; }
        public void FailRange() => _failure.SetResult();
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (Phase == 1)
            {
                await _failure.Task;
                return new HttpResponseMessage(HttpStatusCode.InternalServerError);
            }
            var value = Phase == 0 ? 100m : 200m;
            var path = request.RequestUri!.AbsolutePath;
            object data = path.Contains("GetMoneyWeightedReturn", StringComparison.Ordinal)
                ? new MoneyWeightedReturnResult(value / 500m, MoneyWeightedReturnStatus.Available, _start, _end)
                : path.Contains("GetTimeWeightedReturn", StringComparison.Ordinal)
                ? new TimeWeightedReturnResult(value / 500m, TimeWeightedReturnStatus.Available, _start, _end)
                : path.Contains("GetReturnAttribution", StringComparison.Ordinal)
                ? PortfolioReturnAttributionResult.Available(value, 0m, value, 0m, 0m, _start, _end)
                : path.Contains("TimeSeries", StringComparison.Ordinal)
                ? new List<TimeSeriesModel> { new(_start, value) }
                : new List<NameValueResult> { new("Account", value) };
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(data, data.GetType()) };
        }
    }
}