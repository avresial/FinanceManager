using ApexCharts;
using Bunit;
using Bunit.TestDoubles;
using FinanceManager.Components.Features.MoneyFlow.Components;
using FinanceManager.Components.Features.MoneyFlow.HttpClients;
using FinanceManager.Components.Features.MoneyFlow.Models;
using FinanceManager.Components.Shared.Services;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.Identity.Entities;
using FinanceManager.Domain.Identity.Services;
using FinanceManager.Domain.Labels.Entities;
using FinanceManager.Domain.MoneyFlow.Entities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MudBlazor.Services;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;

namespace FinanceManager.Tests.Unit.Components.Features.MoneyFlow.Components;

[Trait("Category", "Unit")]
public class CashFlowForecastPageTests
{
    private const string _key = "cash-flow-forecast-page:7:0:90";

    [Fact]
    public async Task SnapshotHit_PaintsFullForecastBeforeFetching_AndUnchangedRefreshDoesNotWrite()
    {
        var snapshots = Snapshots(Forecast("CACHED", 90));
        var handler = new DeferredForecastHandler();
        await using var context = CreateContext(handler, snapshots);

        var cut = context.Render<CashFlowForecastPage>();
        await handler.Started(90);
        cut.WaitForAssertion(() =>
        {
            Assert.Contains("CACHED", cut.Markup);
            Assert.Contains("Expected activity", cut.Markup);
            Assert.Contains("Salary", cut.Markup);
            Assert.Contains("+100.00 PLN", cut.Markup);
            Assert.DoesNotContain("mud-skeleton", cut.Markup);
        });

        var renderCount = cut.RenderCount;
        var chart = cut.FindComponent<Stub<ApexChart<TimeSeriesModel>>>().Instance;
        handler.Complete(Forecast("CACHED", 90));
        cut.WaitForState(() => cut.RenderCount > renderCount);
        Assert.Same(chart, cut.FindComponent<Stub<ApexChart<TimeSeriesModel>>>().Instance);
        snapshots.Verify(service => service.SetAsync(It.IsAny<string>(), It.IsAny<CashFlowForecastPageSnapshot>()), Times.Never);
    }

    [Fact]
    public async Task SnapshotMiss_ShowsSkeletonThenPersistsCompleteForecast()
    {
        var snapshots = new Mock<ISnapshotService>();
        var handler = new DeferredForecastHandler();
        await using var context = CreateContext(handler, snapshots);

        var cut = context.Render<CashFlowForecastPage>();
        await handler.Started(90);
        Assert.Contains("mud-skeleton", cut.Markup);

        handler.Complete(Forecast("FRESH", 90));
        cut.WaitForAssertion(() =>
        {
            Assert.Contains("FRESH", cut.Markup);
            Assert.DoesNotContain("mud-skeleton", cut.Markup);
            snapshots.Verify(service => service.SetAsync(_key, It.Is<CashFlowForecastPageSnapshot>(snapshot =>
                snapshot.UserId == 7 && snapshot.CurrencyId == 0 && snapshot.HorizonDays == 90
                && snapshot.Model != null && snapshot.Model.Currency == "PLN"
                && snapshot.Model.Forecast.Currency == "FRESH"
                && snapshot.Model.Forecast.AsOfDate == new DateTime(2026, 9, 16)
                && snapshot.Model.Forecast.HistoricalSeries.Count == 1
                && snapshot.Model.Forecast.ForecastSeries.Count == 2
                && snapshot.Model.Forecast.ExpectedTransactions.Count == 1)), Times.Once);
        });
    }

    [Theory]
    [InlineData(8, 0, 90, 90)]
    [InlineData(7, 1, 90, 90)]
    [InlineData(7, 0, 30, 30)]
    [InlineData(7, 0, 60, 60)]
    [InlineData(7, 0, 90, 30)]
    public async Task MismatchedSnapshot_IsNotPainted(int userId, int currencyId, int horizonDays, int forecastHorizon)
    {
        var snapshot = Snapshot(Forecast("WRONG", forecastHorizon));
        snapshot.UserId = userId;
        snapshot.CurrencyId = currencyId;
        snapshot.HorizonDays = horizonDays;
        var snapshots = new Mock<ISnapshotService>();
        snapshots.Setup(service => service.GetAsync<CashFlowForecastPageSnapshot>(_key)).ReturnsAsync(snapshot);
        var handler = new DeferredForecastHandler();
        await using var context = CreateContext(handler, snapshots);

        var cut = context.Render<CashFlowForecastPage>();
        await handler.Started(90);
        Assert.DoesNotContain("WRONG", cut.Markup);
        Assert.Contains("mud-skeleton", cut.Markup);
        handler.Complete(Forecast("FRESH", 90));
        cut.WaitForAssertion(() => Assert.Contains("FRESH", cut.Markup));
    }

    [Fact]
    public async Task ChangedRefresh_ReplacesPaintedForecastAndSnapshot()
    {
        var snapshots = Snapshots(Forecast("CACHED", 90));
        var handler = new DeferredForecastHandler();
        await using var context = CreateContext(handler, snapshots);

        var cut = context.Render<CashFlowForecastPage>();
        await handler.Started(90);
        Assert.Contains("CACHED", cut.Markup);
        var chart = cut.FindComponent<Stub<ApexChart<TimeSeriesModel>>>().Instance;

        var fresh = Forecast("FRESH", 90);
        fresh.ExpectedTransactions[0].Description = "Updated salary";
        handler.Complete(fresh);
        cut.WaitForAssertion(() =>
        {
            Assert.Contains("FRESH", cut.Markup);
            Assert.Contains("Updated salary", cut.Markup);
            Assert.DoesNotContain("CACHED", cut.Markup);
            Assert.NotSame(chart, cut.FindComponent<Stub<ApexChart<TimeSeriesModel>>>().Instance);
            snapshots.Verify(service => service.SetAsync(_key, It.Is<CashFlowForecastPageSnapshot>(snapshot =>
                snapshot.Model != null && snapshot.Model.Forecast.Currency == "FRESH")), Times.Once);
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RefreshFailure_KeepsSnapshotAndShowsRefreshError_ThenRetryRecovers(bool emptyResponse)
    {
        var snapshots = Snapshots(Forecast("CACHED", 90));
        var handler = new DeferredForecastHandler();
        await using var context = CreateContext(handler, snapshots);

        var cut = context.Render<CashFlowForecastPage>();
        await handler.Started(90);
        if (emptyResponse) handler.Empty(90);
        else handler.Fail(90);
        cut.WaitForAssertion(() =>
        {
            Assert.Contains("CACHED", cut.Markup);
            Assert.Contains("Salary", cut.Markup);
            Assert.Contains("Unable to refresh", cut.Markup);
            Assert.DoesNotContain("mud-skeleton", cut.Markup);
        });
        snapshots.Verify(service => service.SetAsync(It.IsAny<string>(), It.IsAny<CashFlowForecastPageSnapshot>()), Times.Never);

        handler.Reset(90);
        snapshots.Setup(service => service.GetAsync<CashFlowForecastPageSnapshot>(_key))
            .ReturnsAsync((CashFlowForecastPageSnapshot?)null);
        var retry = cut.InvokeAsync(() => cut.FindAll("button").Single(button => button.TextContent.Contains("Retry", StringComparison.Ordinal)).ClickAsync(new()));
        await handler.Started(90);
        Assert.Contains("CACHED", cut.Markup);
        Assert.DoesNotContain("mud-skeleton", cut.Markup);
        handler.Complete(Forecast("RECOVERED", 90));
        await retry;
        Assert.Contains("RECOVERED", cut.Markup);
        Assert.DoesNotContain("Unable to refresh", cut.Markup);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task IncompleteSnapshot_FallsBackToFreshForecast(int missingField)
    {
        var forecast = Forecast("INCOMPLETE", 90);
        var snapshot = Snapshot(forecast);
        if (missingField == 0) snapshot.Model = null;
        else if (missingField == 1) snapshot.Model = new CashFlowForecastPageModel(null!, "PLN");
        else forecast.ForecastSeries = null!;
        var snapshots = new Mock<ISnapshotService>();
        snapshots.Setup(service => service.GetAsync<CashFlowForecastPageSnapshot>(_key)).ReturnsAsync(snapshot);
        var handler = new DeferredForecastHandler();
        await using var context = CreateContext(handler, snapshots);
        var cut = context.Render<CashFlowForecastPage>();
        await handler.Started(90);
        Assert.Contains("mud-skeleton", cut.Markup);
        Assert.DoesNotContain("INCOMPLETE", cut.Markup);
        handler.Complete(Forecast("FRESH", 90));
        cut.WaitForAssertion(() => Assert.Contains("FRESH", cut.Markup));
    }

    [Fact]
    public async Task SupersededFreshResponse_CannotRepaintOrWritePreviousHorizon()
    {
        var snapshots = Snapshots(Forecast("INITIAL", 90));
        var handler = new DeferredForecastHandler();
        await using var context = CreateContext(handler, snapshots);
        var cut = context.Render<CashFlowForecastPage>();
        await handler.Started(90);
        handler.Complete(Forecast("INITIAL", 90));

        var staleSelection = ClickHorizon(cut, 60);
        await handler.Started(60);
        var latestSelection = ClickHorizon(cut, 30);
        await handler.Started(30);
        handler.Complete(Forecast("LATEST", 30));
        await latestSelection;

        handler.Complete(Forecast("STALE", 60));
        await staleSelection;
        Assert.Contains("LATEST", cut.Markup);
        Assert.DoesNotContain("STALE", cut.Markup);
        Assert.DoesNotContain("Unable to", cut.Markup);
        snapshots.Verify(service => service.SetAsync("cash-flow-forecast-page:7:0:60", It.IsAny<CashFlowForecastPageSnapshot>()), Times.Never);
    }

    [Fact]
    public async Task FailureWithoutSnapshot_ShowsBlockingError()
    {
        var handler = new DeferredForecastHandler();
        await using var context = CreateContext(handler);
        var cut = context.Render<CashFlowForecastPage>();
        await handler.Started(90);
        handler.Fail(90);

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Unable to load", cut.Markup);
            Assert.DoesNotContain("mud-skeleton", cut.Markup);
            Assert.DoesNotContain("Projected cash balance", cut.Markup);
        });
    }

    [Theory]
    [InlineData(30)]
    [InlineData(60)]
    public async Task SelectingHorizon_PaintsMatchingSnapshotThenRefreshes(int horizonDays)
    {
        var snapshots = Snapshots(Forecast("INITIAL", 90));
        var key = $"cash-flow-forecast-page:7:0:{horizonDays}";
        snapshots.Setup(service => service.GetAsync<CashFlowForecastPageSnapshot>(key))
            .ReturnsAsync(Snapshot(Forecast("MATCHING", horizonDays)));
        var handler = new DeferredForecastHandler();
        await using var context = CreateContext(handler, snapshots);
        var cut = context.Render<CashFlowForecastPage>();
        await handler.Started(90);
        handler.Complete(Forecast("INITIAL", 90));
        cut.WaitForState(() => cut.RenderCount >= 3);

        var selection = ClickHorizon(cut, horizonDays);
        await handler.Started(horizonDays);
        Assert.Contains("MATCHING", cut.Markup);
        Assert.DoesNotContain("INITIAL", cut.Markup);
        Assert.DoesNotContain("mud-skeleton", cut.Markup);

        handler.Complete(Forecast("REFRESHED", horizonDays));
        await selection;
        Assert.Contains("REFRESHED", cut.Markup);
        snapshots.Verify(service => service.SetAsync(key, It.Is<CashFlowForecastPageSnapshot>(snapshot =>
            snapshot.HorizonDays == horizonDays && snapshot.Model != null
            && snapshot.Model.Forecast.HorizonDays == horizonDays)), Times.Once);
    }

    [Fact]
    public async Task SelectingHorizonWithoutSnapshot_HidesPreviousForecastUntilFreshDataArrives()
    {
        var handler = new DeferredForecastHandler();
        await using var context = CreateContext(handler, Snapshots(Forecast("INITIAL", 90)));
        var cut = context.Render<CashFlowForecastPage>();
        await handler.Started(90);
        handler.Complete(Forecast("INITIAL", 90));

        var selection = ClickHorizon(cut, 30);
        await handler.Started(30);
        Assert.DoesNotContain("INITIAL", cut.Markup);
        Assert.Contains("mud-skeleton", cut.Markup);
        handler.Complete(Forecast("LATEST", 30));
        await selection;
        Assert.Contains("LATEST", cut.Markup);
    }

    [Fact]
    public async Task SupersededSnapshotRead_CannotPaintOrFetchPreviousHorizon()
    {
        var snapshots = Snapshots(Forecast("INITIAL", 90));
        var staleRead = new TaskCompletionSource<CashFlowForecastPageSnapshot?>(TaskCreationOptions.RunContinuationsAsynchronously);
        snapshots.Setup(service => service.GetAsync<CashFlowForecastPageSnapshot>("cash-flow-forecast-page:7:0:60"))
            .Returns(staleRead.Task);
        snapshots.Setup(service => service.GetAsync<CashFlowForecastPageSnapshot>("cash-flow-forecast-page:7:0:30"))
            .ReturnsAsync(Snapshot(Forecast("LATEST", 30)));
        var handler = new DeferredForecastHandler();
        await using var context = CreateContext(handler, snapshots);
        var cut = context.Render<CashFlowForecastPage>();
        await handler.Started(90);
        handler.Complete(Forecast("INITIAL", 90));

        var staleSelection = ClickHorizon(cut, 60);
        cut.WaitForAssertion(() => snapshots.Verify(service => service.GetAsync<CashFlowForecastPageSnapshot>("cash-flow-forecast-page:7:0:60"), Times.Once));
        var latestSelection = ClickHorizon(cut, 30);
        await handler.Started(30);
        Assert.Contains("LATEST", cut.Markup);

        staleRead.SetResult(Snapshot(Forecast("STALE", 60)));
        await staleSelection;
        Assert.Contains("LATEST", cut.Markup);
        Assert.DoesNotContain("STALE", cut.Markup);
        Assert.DoesNotContain(60, handler.RequestedHorizons);
        snapshots.Verify(service => service.SetAsync("cash-flow-forecast-page:7:0:60", It.IsAny<CashFlowForecastPageSnapshot>()), Times.Never);
        handler.Complete(Forecast("LATEST", 30));
        await latestSelection;
    }

    [Fact]
    public async Task NoActivityRefresh_ReplacesPreviouslyPopulatedSnapshot()
    {
        var snapshots = Snapshots(Forecast("CACHED", 90));
        var handler = new DeferredForecastHandler();
        await using var context = CreateContext(handler, snapshots);
        var cut = context.Render<CashFlowForecastPage>();
        await handler.Started(90);
        var forecast = Forecast("EMPTY", 90);
        forecast.HasForecastableActivity = false;
        forecast.ExpectedTransactions = [];
        handler.Complete(forecast);

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("No recurring activity", cut.Markup);
            Assert.Contains("Projected cash balance", cut.Markup);
            Assert.DoesNotContain("Expected activity", cut.Markup);
            Assert.DoesNotContain("Salary", cut.Markup);
            snapshots.Verify(service => service.SetAsync(_key, It.Is<CashFlowForecastPageSnapshot>(snapshot =>
                snapshot.Model != null && !snapshot.Model.Forecast.HasForecastableActivity
                && snapshot.Model.Forecast.ExpectedTransactions.Count == 0)), Times.Once);
        });
    }

    [Fact]
    public async Task SelectingNewHorizon_CancelsPreviousLoadAndKeepsLatestForecast()
    {
        var handler = new ForecastHandler();
        await using var context = CreateContext(handler);
        var cut = context.Render<CashFlowForecastPage>();
        cut.WaitForAssertion(() => Assert.Contains("INITIAL", cut.Markup));

        var staleSelection = ClickHorizon(cut, 60);
        await handler.SixtyDayRequestStarted.Task.WaitAsync(
            TimeSpan.FromSeconds(1),
            Xunit.TestContext.Current.CancellationToken);

        await ClickHorizon(cut, 30);
        await staleSelection.WaitAsync(TimeSpan.FromSeconds(1), Xunit.TestContext.Current.CancellationToken);

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("LATEST", cut.Markup);
            Assert.DoesNotContain("STALE", cut.Markup);
            Assert.DoesNotContain("mud-skeleton", cut.Markup);
        });
        Assert.True(handler.SixtyDayRequestCancelled.Task.IsCompletedSuccessfully);
    }

    private static AngleSharp.Dom.IElement FindHorizon(IRenderedComponent<CashFlowForecastPage> cut, int horizonDays) =>
        cut.FindAll("button").Single(button => button.TextContent.Contains($"{horizonDays} days", StringComparison.Ordinal));

    private static Task ClickHorizon(IRenderedComponent<CashFlowForecastPage> cut, int horizonDays) =>
        cut.InvokeAsync(() => FindHorizon(cut, horizonDays).ClickAsync(new()));

    private static BunitContext CreateContext(HttpMessageHandler handler, Mock<ISnapshotService>? snapshots = null)
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.ComponentFactories.AddStub<ApexChart<TimeSeriesModel>>();
        context.Services.AddLogging();
        context.Services.AddMudServices();

        var login = new Mock<ILoginService>();
        login.Setup(service => service.GetLoggedUser()).ReturnsAsync(new UserSession
        {
            UserId = 7,
            UserName = "guest",
            Password = string.Empty,
            UserRole = UserRole.User
        });
        context.Services.AddSingleton(login.Object);

        var settings = new Mock<ISettingsService>();
        settings.Setup(service => service.GetCurrencyAsync()).ReturnsAsync(DefaultCurrency.PLN);
        context.Services.AddSingleton(settings.Object);
        context.Services.AddSingleton<ISnapshotRefreshCoordinator>(new SnapshotRefreshCoordinator(
            (snapshots ?? new Mock<ISnapshotService>()).Object, NullLogger<SnapshotRefreshCoordinator>.Instance));

        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") };
        context.Services.AddSingleton(new CashFlowForecastHttpClient(httpClient));
        return context;
    }

    private static Mock<ISnapshotService> Snapshots(CashFlowForecast forecast)
    {
        var snapshots = new Mock<ISnapshotService>();
        snapshots.Setup(service => service.GetAsync<CashFlowForecastPageSnapshot>(_key)).ReturnsAsync(Snapshot(forecast));
        return snapshots;
    }

    private static CashFlowForecastPageSnapshot Snapshot(CashFlowForecast forecast) => new()
    {
        UserId = 7,
        CurrencyId = 0,
        HorizonDays = forecast.HorizonDays,
        Model = new CashFlowForecastPageModel(forecast, "PLN")
    };

    private static CashFlowForecast Forecast(string currency, int horizonDays) => new()
    {
        UserId = 7,
        CurrencyId = 0,
        Currency = currency,
        HorizonDays = horizonDays,
        AsOfDate = new DateTime(2026, 9, 16),
        HasForecastableActivity = true,
        HistoricalSeries = [new TimeSeriesModel(new DateTime(2026, 9, 15), 50m)],
        ForecastSeries = [new TimeSeriesModel(new DateTime(2026, 9, 16), 100m), new TimeSeriesModel(new DateTime(2026, 9, 16).AddDays(horizonDays), 200m)],
        ExpectedTransactions = [new CashFlowForecastTransaction { Description = "Salary", Date = new DateTime(2026, 9, 17), Amount = 100m, Cadence = RecurringCadence.Monthly }]
    };

    private sealed class DeferredForecastHandler : HttpMessageHandler
    {
        private readonly ConcurrentDictionary<int, TaskCompletionSource<HttpResponseMessage>> _responses = [];
        private readonly ConcurrentDictionary<int, TaskCompletionSource> _started = [];
        public ConcurrentBag<int> RequestedHorizons { get; } = [];

        public void Reset(int horizonDays)
        {
            _responses[horizonDays] = new(TaskCreationOptions.RunContinuationsAsynchronously);
            _started[horizonDays] = new(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        public Task Started(int horizonDays)
        {
            return _started.GetOrAdd(horizonDays, _ => new(TaskCreationOptions.RunContinuationsAsynchronously))
                .Task.WaitAsync(TimeSpan.FromSeconds(5), Xunit.TestContext.Current.CancellationToken);
        }

        public void Complete(CashFlowForecast forecast) => _responses[forecast.HorizonDays].TrySetResult(new(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(forecast)
        });

        public void Fail(int horizonDays) => _responses[horizonDays].TrySetException(new HttpRequestException("Test refresh failure"));

        public void Empty(int horizonDays) => _responses[horizonDays].TrySetResult(new(HttpStatusCode.OK)
        {
            Content = JsonContent.Create<CashFlowForecast?>(null)
        });

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var horizon = CashFlowForecastHorizons.All.Single(days => request.RequestUri!.Query.Contains($"horizonDays={days}", StringComparison.Ordinal));
            var response = _responses.GetOrAdd(horizon, _ => new(TaskCreationOptions.RunContinuationsAsynchronously));
            RequestedHorizons.Add(horizon);
            _started.GetOrAdd(horizon, _ => new(TaskCreationOptions.RunContinuationsAsynchronously)).TrySetResult();
            return response.Task;
        }
    }

    private sealed class ForecastHandler : HttpMessageHandler
    {
        public TaskCompletionSource SixtyDayRequestStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource SixtyDayRequestCancelled { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var query = request.RequestUri!.Query;
            if (query.Contains("horizonDays=60", StringComparison.Ordinal))
            {
                SixtyDayRequestStarted.TrySetResult();
                try
                {
                    await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    SixtyDayRequestCancelled.TrySetResult();
                    throw;
                }

                return Forecast("STALE", 60);
            }

            return query.Contains("horizonDays=30", StringComparison.Ordinal)
                ? Forecast("LATEST", 30)
                : Forecast("INITIAL", 90);
        }

        private static HttpResponseMessage Forecast(string currency, int horizonDays) => new(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new CashFlowForecast
            {
                Currency = currency,
                HorizonDays = horizonDays,
                AsOfDate = new DateTime(2026, 9, 16),
                HistoricalSeries = [new TimeSeriesModel(new DateTime(2026, 9, 16), 100m)],
                ForecastSeries = [new TimeSeriesModel(new DateTime(2026, 9, 16), 100m)]
            })
        };
    }
}