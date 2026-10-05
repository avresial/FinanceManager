using Blazored.LocalStorage;
using Bunit;
using FinanceManager.Components.Features.Dashboard.Components.Cards.Assets;
using FinanceManager.Components.Features.Dashboard.Models;
using FinanceManager.Components.Features.Dashboard.Services;
using FinanceManager.Components.Features.MoneyFlow.HttpClients;
using FinanceManager.Components.Shared.Services;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.Identity.Entities;
using FinanceManager.Domain.Identity.Services;
using FinanceManager.Domain.MoneyFlow.Entities;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MudBlazor.Services;
using System.Net;
using System.Net.Http.Json;
using System.Text;

namespace FinanceManager.Tests.Unit.Components.Features.Dashboard.Components.Cards.Assets;

[Trait("Category", "Unit")]
public class PortfolioReturnCardTests
{
    private static readonly DateTime _start = new(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime _end = _start.AddDays(6);

    [Fact]
    public async Task BothAvailable_ShowsMetricsAndFactualSummary()
    {
        await using var context = CreateContext(new ReturnsHandler(
            new(0.2m, MoneyWeightedReturnStatus.Available, _start, _end),
            new(0.1m, TimeWeightedReturnStatus.Available, _start, _end),
            PortfolioReturnAttributionResult.Available(300m, 100m, 200m, 0m, 0m, _start, _end)));

        var cut = Render(context);
        cut.WaitForAssertion(() => Assert.Contains("20.00%", cut.Markup));

        Assert.Contains("10.00%", cut.Markup);
        Assert.Contains("Net external inflows of", cut.Markup);
        Assert.Contains("Net external cash flow", cut.Markup);
        Assert.Contains("Period length", cut.Markup);
        Assert.Contains("7 days", cut.Markup);
        Assert.Contains("Annualized · PLN", cut.Markup);
        Assert.Contains("Cumulative · PLN", cut.Markup);
        Assert.Equal(1, cut.Markup.Split("1–7 Aug 2026").Length - 1);
        Assert.Equal(2, cut.FindAll(".portfolio-return-metric").Count);
        Assert.Equal(2, cut.FindAll(".portfolio-return-fact").Count);
    }

    [Fact]
    public async Task OneUnavailable_KeepsAvailableSibling()
    {
        await using var context = CreateContext(new ReturnsHandler(
            new(null, MoneyWeightedReturnStatus.InsufficientData, _start, _end),
            new(0.1m, TimeWeightedReturnStatus.Available, _start, _end),
            null));

        var cut = Render(context);
        cut.WaitForAssertion(() => Assert.Contains("10.00%", cut.Markup));

        Assert.Contains("Insufficient data", cut.Markup);
        Assert.DoesNotContain("Neither return is available", cut.Markup);
        Assert.Contains("Cash-flow timing affects annualized XIRR", cut.Markup);
    }

    [Fact]
    public async Task BothUnavailable_ShowsUnifiedEmptyStateAndMetricReasons()
    {
        await using var context = CreateContext(new ReturnsHandler(
            new(null, MoneyWeightedReturnStatus.NoSolution, _start, _end),
            new(null, TimeWeightedReturnStatus.InsufficientData, _start, _end),
            null));

        var cut = Render(context);
        cut.WaitForAssertion(() => Assert.Contains("Neither return is available", cut.Markup));

        Assert.Contains("No solution", cut.Markup);
        Assert.Contains("Insufficient data", cut.Markup);
    }

    [Fact]
    public async Task FailedMetricRequest_DoesNotDiscardSibling()
    {
        await using var context = CreateContext(new ReturnsHandler(
            new(0.2m, MoneyWeightedReturnStatus.Available, _start, _end),
            new(0.1m, TimeWeightedReturnStatus.Available, _start, _end),
            null,
            failMoneyWeightedOnce: true));

        var cut = Render(context);
        cut.WaitForAssertion(() => Assert.Contains("10.00%", cut.Markup));

        Assert.Contains("Could not load this return", cut.Markup);
        Assert.Contains("Retry unavailable return", cut.Markup);
        Assert.DoesNotContain("Neither return is available", cut.Markup);

        cut.FindAll("button").Single(button => button.TextContent.Contains("Retry unavailable return", StringComparison.Ordinal)).Click();
        cut.WaitForAssertion(() => Assert.Contains("20.00%", cut.Markup));
        Assert.Contains("10.00%", cut.Markup);
    }

    [Fact]
    public async Task FailedMetricRequest_NormalReloadFetchesRecoveredResult()
    {
        var handler = new ReturnsHandler(
            new(0.2m, MoneyWeightedReturnStatus.Available, _start, _end),
            new(0.1m, TimeWeightedReturnStatus.Available, _start, _end),
            null,
            failMoneyWeightedOnce: true);
        await using var context = CreateContext(handler);

        var cut = Render(context);
        cut.WaitForAssertion(() => Assert.Contains("Could not load this return", cut.Markup));

        cut.Render(parameters => parameters
            .Add(component => component.StartDateTime, _start)
            .Add(component => component.EndDateTime, _end));

        cut.WaitForAssertion(() => Assert.Contains("20.00%", cut.Markup));
        Assert.Contains("10.00%", cut.Markup);
    }

    [Fact]
    public async Task RangeChange_IgnoresSlowerPreviousSnapshot()
    {
        var secondStart = _end.AddDays(1);
        var secondEnd = secondStart.AddDays(6);
        var handler = new QueuedReturnsHandler();
        await using var context = CreateContext(handler);

        var cut = Render(context);
        cut.Render(parameters => parameters
            .Add(component => component.StartDateTime, secondStart)
            .Add(component => component.EndDateTime, secondEnd));
        Assert.Equal(2, handler.MoneyWeightedRequestCount);
        Assert.Equal(2, handler.TimeWeightedRequestCount);

        handler.Complete(1,
            new(0.2m, MoneyWeightedReturnStatus.Available, secondStart, secondEnd),
            new(0.3m, TimeWeightedReturnStatus.Available, secondStart, secondEnd));
        cut.WaitForAssertion(() =>
        {
            Assert.Contains("20.00%", cut.Markup);
            Assert.Contains("30.00%", cut.Markup);
        });

        var renderCount = cut.RenderCount;
        handler.Complete(0,
            new(0.1m, MoneyWeightedReturnStatus.Available, _start, _end),
            new(0.4m, TimeWeightedReturnStatus.Available, _start, _end));
        cut.WaitForState(() => cut.RenderCount > renderCount);

        Assert.Contains("20.00%", cut.Markup);
        Assert.Contains("30.00%", cut.Markup);
        Assert.DoesNotContain("10.00%", cut.Markup);
        Assert.DoesNotContain("40.00%", cut.Markup);
    }

    [Fact]
    public async Task RangeChange_HidesPreviousCashFlowWhileLoading()
    {
        var secondStart = _end.AddDays(1);
        var secondEnd = secondStart.AddDays(6);
        var handler = new QueuedReturnsHandler();
        await using var context = CreateContext(handler);

        var cut = Render(context);
        handler.Complete(0,
            new(0.2m, MoneyWeightedReturnStatus.Available, _start, _end),
            new(0.3m, TimeWeightedReturnStatus.Available, _start, _end),
            PortfolioReturnAttributionResult.Available(300m, 100m, 200m, 0m, 0m, _start, _end));
        cut.WaitForAssertion(() => Assert.Contains("Net external cash flow", cut.Markup));

        cut.Render(parameters => parameters
            .Add(component => component.StartDateTime, secondStart)
            .Add(component => component.EndDateTime, secondEnd));
        Assert.Equal(2, handler.MoneyWeightedRequestCount);
        Assert.DoesNotContain("Net external cash flow", cut.Markup);
        Assert.Single(cut.FindAll(".portfolio-return-fact"));

        handler.Complete(1,
            new(0.1m, MoneyWeightedReturnStatus.Available, secondStart, secondEnd),
            new(0.4m, TimeWeightedReturnStatus.Available, secondStart, secondEnd),
            PortfolioReturnAttributionResult.Available(400m, 200m, 200m, 0m, 0m, secondStart, secondEnd));
        cut.WaitForAssertion(() => Assert.Contains("Net external cash flow", cut.Markup));
        Assert.Equal(2, cut.FindAll(".portfolio-return-fact").Count);
    }

    [Fact]
    public async Task Hydration_SameCalendarRangePaintsBeforeFreshRequest_EqualContentDoesNotWrite()
    {
        var model = Model(0.2m, 0.1m);
        var snapshots = Snapshots(model);
        var handler = new QueuedReturnsHandler();
        await using var context = CreateContext(handler, snapshots.Object);
        var cut = Render(context);
        cut.WaitForAssertion(() => Assert.Contains("20.00%", cut.Markup));
        Assert.Equal(1, handler.MoneyWeightedRequestCount);
        var renders = cut.RenderCount;
        handler.Complete(0, model.MoneyWeightedReturn!, model.TimeWeightedReturn!, model.Attribution);
        cut.WaitForState(() => cut.RenderCount > renders);
        snapshots.Verify(service => service.SetAsync(It.IsAny<string>(), It.IsAny<PortfolioReturnCardSnapshot>()), Times.Never);
    }

    [Fact]
    public async Task FailedRefresh_KeepsPaintedSnapshotWithoutWriting()
    {
        var snapshots = Snapshots(Model(0.5m, 0.4m));
        await using var context = CreateContext(new ReturnsHandler(
            new(0.2m, MoneyWeightedReturnStatus.Available, _start, _end),
            new(0.1m, TimeWeightedReturnStatus.Available, _start, _end), null, true), snapshots.Object);
        var cut = Render(context);
        cut.WaitForAssertion(() => Assert.Contains("50.00%", cut.Markup));
        Assert.Contains("40.00%", cut.Markup);
        snapshots.Verify(service => service.SetAsync(It.IsAny<string>(), It.IsAny<PortfolioReturnCardSnapshot>()), Times.Never);
    }

    [Fact]
    public async Task SuccessfulNoData_ReplacesPaintedSnapshot()
    {
        var snapshots = Snapshots(Model(0.5m, 0.4m));
        await using var context = CreateContext(new ReturnsHandler(
            new(null, MoneyWeightedReturnStatus.InsufficientData, _start, _end),
            new(null, TimeWeightedReturnStatus.InsufficientData, _start, _end), null), snapshots.Object);
        var cut = Render(context);
        cut.WaitForAssertion(() => Assert.Contains("Neither return is available", cut.Markup));
        Assert.DoesNotContain("50.00%", cut.Markup);
        snapshots.Verify(service => service.SetAsync($"portfolio-return:1:{DefaultCurrency.PLN.Id}", It.Is<PortfolioReturnCardSnapshot>(value => value.Model!.MoneyWeightedStatus == MoneyWeightedReturnStatus.InsufficientData)), Times.Once);
    }

    [Fact]
    public async Task BrokenStorage_StillRendersFreshReturns()
    {
        var snapshots = new Mock<ISnapshotService>();
        snapshots.Setup(service => service.GetAsync<PortfolioReturnCardSnapshot>(It.IsAny<string>())).ThrowsAsync(new InvalidOperationException("storage"));
        snapshots.Setup(service => service.SetAsync(It.IsAny<string>(), It.IsAny<PortfolioReturnCardSnapshot>())).ThrowsAsync(new InvalidOperationException("storage"));
        await using var context = CreateContext(new ReturnsHandler(
            new(0.2m, MoneyWeightedReturnStatus.Available, _start, _end),
            new(0.1m, TimeWeightedReturnStatus.Available, _start, _end), null), snapshots.Object);
        var cut = Render(context);
        cut.WaitForAssertion(() => Assert.Contains("20.00%", cut.Markup));
    }

    [Fact]
    public async Task ConcurrentSourceLoads_ShareRequest_AndLaterVisitFetchesAgain()
    {
        var handler = new QueuedReturnsHandler();
        await using var context = CreateContext(handler);
        var cache = context.Services.GetRequiredService<AssetsPageCardsCacheService>();
        var range = new AssetsPageCardsRefreshContext { UserId = 1, CurrencyId = 1, StartDateTime = _start, EndDateTime = _end };
        var first = cache.GetFreshReturnsAsync(range);
        var second = cache.GetFreshReturnsAsync(range);
        Assert.Equal(1, handler.MoneyWeightedRequestCount);
        handler.Complete(0, Model(0.2m, 0.1m).MoneyWeightedReturn!, Model(0.2m, 0.1m).TimeWeightedReturn!);
        Assert.Same(await first, await second);
        var later = cache.GetFreshReturnsAsync(range);
        Assert.Equal(2, handler.MoneyWeightedRequestCount);
        handler.Complete(1, Model(0.3m, 0.2m).MoneyWeightedReturn!, Model(0.3m, 0.2m).TimeWeightedReturn!);
        await later;
    }

    private static PortfolioReturnSourceModel Model(decimal money, decimal time) => new(
        new(money, MoneyWeightedReturnStatus.Available, _start, _end),
        new(time, TimeWeightedReturnStatus.Available, _start, _end),
        PortfolioReturnAttributionResult.Unavailable(_start, _end));

    private static Mock<ISnapshotService> Snapshots(PortfolioReturnSourceModel model)
    {
        var snapshots = new Mock<ISnapshotService>();
        snapshots.Setup(service => service.GetAsync<PortfolioReturnCardSnapshot>(It.IsAny<string>())).ReturnsAsync(new PortfolioReturnCardSnapshot
        {
            UserId = 1,
            CurrencyId = DefaultCurrency.PLN.Id,
            StartDateTime = _start,
            EndDateTime = _end.AddHours(1),
            Model = PortfolioReturnCardModel.FromSource(model),
        });
        return snapshots;
    }

    private static IRenderedComponent<PortfolioReturnCardContainer> Render(BunitContext context) =>
        context.Render<PortfolioReturnCardContainer>(parameters => parameters
            .Add(component => component.StartDateTime, _start)
            .Add(component => component.EndDateTime, _end));

    private static BunitContext CreateContext(HttpMessageHandler handler, ISnapshotService? snapshots = null)
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddLogging();
        context.Services.AddSingleton<ISnapshotRefreshCoordinator>(new SnapshotRefreshCoordinator(snapshots ?? Mock.Of<ISnapshotService>(), NullLogger<SnapshotRefreshCoordinator>.Instance));
        context.Services.AddMudServices();

        var settings = new Mock<ISettingsService>();
        settings.Setup(service => service.GetCurrencyAsync()).ReturnsAsync(DefaultCurrency.PLN);
        context.Services.AddSingleton(settings.Object);

        var login = new Mock<ILoginService>();
        login.Setup(service => service.GetLoggedUser()).ReturnsAsync(new UserSession
        {
            UserId = 1,
            UserName = "tester",
            Password = string.Empty,
            UserRole = UserRole.User,
        });
        context.Services.AddSingleton(login.Object);

        context.Services.AddSingleton(new AssetsPageCardsCacheService(
            Mock.Of<ILocalStorageService>(),
            new MemoryCache(new MemoryCacheOptions()),
            new AssetsHttpClient(new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") }),
            NullLogger<AssetsPageCardsCacheService>.Instance));

        return context;
    }

    private sealed class ReturnsHandler(
        MoneyWeightedReturnResult? moneyWeighted,
        TimeWeightedReturnResult timeWeighted,
        PortfolioReturnAttributionResult? attribution,
        bool failMoneyWeightedOnce = false) : HttpMessageHandler
    {
        private int _moneyWeightedRequestCount;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri?.AbsolutePath ?? string.Empty;
            if (path.Contains("GetMoneyWeightedReturn", StringComparison.Ordinal))
                return Task.FromResult(failMoneyWeightedOnce && Interlocked.Increment(ref _moneyWeightedRequestCount) == 1
                    ? new HttpResponseMessage(HttpStatusCode.InternalServerError)
                    : Response(moneyWeighted));
            if (path.Contains("GetTimeWeightedReturn", StringComparison.Ordinal))
                return Task.FromResult(Response(timeWeighted));
            if (path.Contains("GetReturnAttribution", StringComparison.Ordinal))
                return Task.FromResult(Response(attribution ?? PortfolioReturnAttributionResult.Unavailable(_start, _end)));

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("[]", Encoding.UTF8, "application/json"),
            });
        }

        private static HttpResponseMessage Response<T>(T value) => new(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(value),
        };
    }

    private sealed class QueuedReturnsHandler : HttpMessageHandler
    {
        private readonly TaskCompletionSource<HttpResponseMessage>[] _moneyWeightedResponses =
        [
            new(TaskCreationOptions.RunContinuationsAsynchronously),
            new(TaskCreationOptions.RunContinuationsAsynchronously),
        ];
        private readonly TaskCompletionSource<HttpResponseMessage>[] _timeWeightedResponses =
        [
            new(TaskCreationOptions.RunContinuationsAsynchronously),
            new(TaskCreationOptions.RunContinuationsAsynchronously),
        ];
        private readonly TaskCompletionSource<HttpResponseMessage>[] _attributionResponses =
        [
            new(TaskCreationOptions.RunContinuationsAsynchronously),
            new(TaskCreationOptions.RunContinuationsAsynchronously),
        ];
        private int _moneyWeightedRequestCount;
        private int _timeWeightedRequestCount;
        private int _attributionRequestCount;

        public int MoneyWeightedRequestCount => Volatile.Read(ref _moneyWeightedRequestCount);
        public int TimeWeightedRequestCount => Volatile.Read(ref _timeWeightedRequestCount);

        public void Complete(
            int index,
            MoneyWeightedReturnResult moneyWeighted,
            TimeWeightedReturnResult timeWeighted,
            PortfolioReturnAttributionResult? attribution = null)
        {
            _moneyWeightedResponses[index].SetResult(Response(moneyWeighted));
            _timeWeightedResponses[index].SetResult(Response(timeWeighted));
            _attributionResponses[index].SetResult(Response(attribution ?? PortfolioReturnAttributionResult.Unavailable(_start, _end)));
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri?.AbsolutePath ?? string.Empty;
            if (path.Contains("GetMoneyWeightedReturn", StringComparison.Ordinal))
                return _moneyWeightedResponses[Interlocked.Increment(ref _moneyWeightedRequestCount) - 1].Task;
            if (path.Contains("GetTimeWeightedReturn", StringComparison.Ordinal))
                return _timeWeightedResponses[Interlocked.Increment(ref _timeWeightedRequestCount) - 1].Task;
            if (path.Contains("GetReturnAttribution", StringComparison.Ordinal))
                return _attributionResponses[Interlocked.Increment(ref _attributionRequestCount) - 1].Task;

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("[]", Encoding.UTF8, "application/json"),
            });
        }

        private static HttpResponseMessage Response<T>(T value) => new(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(value),
        };
    }
}