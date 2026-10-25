using ApexCharts;
using Bunit;
using FinanceManager.Components.Features.Dashboard.Components.Cards;
using FinanceManager.Components.Features.Dashboard.Models;
using FinanceManager.Components.Features.MoneyFlow.HttpClients;
using FinanceManager.Components.Shared.Services;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.Identity.Entities;
using FinanceManager.Domain.Identity.Services;
using FinanceManager.Domain.MoneyFlow.Entities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MudBlazor.Services;
using System.Net;
using System.Net.Http.Json;

namespace FinanceManager.Tests.Unit.Components.Features.Dashboard.Components.Cards;

[Trait("Category", "Unit")]
public sealed class CashFlowForecastCardTests
{
    private const string _key = "cash-flow-forecast-card:7:0:90";
    private static readonly DateTime _asOfDate = new(2026, 9, 28);

    [Fact]
    public void RenderedModel_KeepsHistoryAndComputesHorizonDeltas()
    {
        var original = Forecast(100m);
        original.ForecastSeries[1].Value = 130m;
        original.ForecastSeries[2].Value = 160m;
        original.ForecastSeries[3].Value = 190m;
        original.ExpectedTransactions =
        [
            new CashFlowForecastTransaction { Amount = 50m },
            new CashFlowForecastTransaction { Amount = -20m },
            new CashFlowForecastTransaction { Amount = -10m }
        ];
        var changedHistory = Forecast(100m);
        changedHistory.ForecastSeries[1].Value = 130m;
        changedHistory.ForecastSeries[2].Value = 160m;
        changedHistory.ForecastSeries[3].Value = 190m;
        changedHistory.ExpectedTransactions = original.ExpectedTransactions;
        changedHistory.HistoricalSeries = [new TimeSeriesModel(_asOfDate.AddDays(-1), 25m)];

        var model = CashFlowForecastCardModel.FromForecast(original, "PLN");
        Assert.Equal(100m, model.CurrentBalance);
        Assert.Equal(130m, model.ThirtyDayValue);
        Assert.Equal(160m, model.SixtyDayValue);
        Assert.Equal(190m, model.NinetyDayValue);
        Assert.Equal(1, model.ExpectedInflowCount);
        Assert.Equal(2, model.ExpectedOutflowCount);
        Assert.Equal(30m, model.ThirtyDayDelta);
        Assert.Equal(60m, model.SixtyDayDelta);
        Assert.Equal(90m, model.NinetyDayDelta);
        Assert.Empty(model.HistoricalPoints!);
        Assert.Single(CashFlowForecastCardModel.FromForecast(changedHistory, "PLN").HistoricalPoints!);
    }

    [Theory]
    [InlineData(25)]
    [InlineData(-25)]
    public void FromForecast_FindsLowestDailyBalanceIncludingLastDay(decimal minimum)
    {
        var forecast = Forecast(100m);
        forecast.ForecastSeries.Add(new TimeSeriesModel(_asOfDate.AddDays(90), minimum));
        forecast.ForecastSeries.Add(new TimeSeriesModel(_asOfDate.AddDays(91), -500m));
        forecast.HistoricalSeries =
        [
            new TimeSeriesModel(_asOfDate.AddDays(-31), -900m),
            new TimeSeriesModel(_asOfDate.AddDays(-30), 80m),
            new TimeSeriesModel(_asOfDate, 100m),
            new TimeSeriesModel(_asOfDate.AddDays(1), 90m)
        ];

        var model = CashFlowForecastCardModel.FromForecast(forecast, "PLN");

        Assert.Equal(minimum, model.LowestBalancePoint!.Value);
        Assert.Equal(_asOfDate.AddDays(90), model.LowestBalancePoint.DateTime);
        Assert.Equal(2, model.HistoricalPoints!.Count);
        Assert.DoesNotContain(model.ForecastPoints!, point => point.DateTime > _asOfDate.AddDays(90));
    }

    [Fact]
    public void FromForecast_EmptySeriesHasNoMinimumOrDeltas()
    {
        var model = CashFlowForecastCardModel.FromForecast(new CashFlowForecast { AsOfDate = _asOfDate }, "PLN");

        Assert.Empty(model.HistoricalPoints!);
        Assert.Empty(model.ForecastPoints!);
        Assert.Null(model.LowestBalancePoint);
        Assert.Equal(0m, model.CurrentBalance);
        Assert.Equal(0m, model.ThirtyDayDelta);
        Assert.Equal(0m, model.SixtyDayDelta);
        Assert.Equal(0m, model.NinetyDayDelta);
    }

    [Fact]
    public async Task OlderSnapshotWithoutSeries_PaintsTotalsAndDerivedDeltasDuringRefreshFailure()
    {
        var snapshot = System.Text.Json.JsonSerializer.Deserialize<CashFlowForecastCardSnapshot>(
            """{"UserId":7,"CurrencyId":0,"HorizonDays":90,"Model":{"Currency":"PLN","CurrentBalance":100,"ThirtyDayValue":130,"SixtyDayValue":60,"NinetyDayValue":100,"HasForecastableActivity":true,"ExpectedInflowCount":1,"ExpectedOutflowCount":2}}""");
        var snapshots = new Mock<ISnapshotService>();
        snapshots.Setup(service => service.GetAsync<CashFlowForecastCardSnapshot>(_key)).ReturnsAsync(snapshot);
        var handler = new DeferredForecastHandler();
        await using var context = CreateContext(snapshots, handler);

        var cut = context.Render<CashFlowForecastCard>();
        await handler.Started.Task.WaitAsync(TimeSpan.FromSeconds(5), Xunit.TestContext.Current.CancellationToken);
        handler.Fail();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("130.00", cut.Markup);
            Assert.Contains(Money(100m), cut.Markup);
            Assert.Contains("+30.00 PLN", cut.Markup);
            Assert.Contains("-40.00 PLN", cut.Markup);
            Assert.Empty(cut.FindAll(".cash-flow-forecast-card__chart"));
            Assert.DoesNotContain("Couldn't load forecast", cut.Markup);
        });
    }

    [Fact]
    public async Task NegativeMinimum_ShowsWarningAndNoActivityHidesChart()
    {
        var forecast = Forecast(100m);
        forecast.ForecastSeries[1].Value = -20m;
        var snapshots = new Mock<ISnapshotService>();
        snapshots.Setup(service => service.GetAsync<CashFlowForecastCardSnapshot>(_key)).ReturnsAsync(Snapshot(forecast));
        var handler = new DeferredForecastHandler();
        await using var context = CreateContext(snapshots, handler);
        var cut = context.Render<CashFlowForecastCard>();
        await handler.Started.Task.WaitAsync(TimeSpan.FromSeconds(5), Xunit.TestContext.Current.CancellationToken);
        cut.WaitForAssertion(() =>
        {
            Assert.Contains("mud-warning-text", cut.Find(".cash-flow-forecast-card__lowest").ClassList);
            Assert.Contains("Below zero", cut.Markup);
            Assert.NotEmpty(cut.FindAll(".cash-flow-forecast-card__chart"));
            var options = cut.FindComponent<ApexChart<TimeSeriesModel>>().Instance.Options;
            Assert.Equal(4, options.Annotations.Xaxis.Count);
            Assert.Equal(new[] { "Today", "30d", "60d", "90d" }, options.Annotations.Xaxis.SelectMany(marker => (List<string>)marker.Label.Text));
            Assert.Equal("var(--mud-palette-warning)", Assert.Single(options.Annotations.Points).Marker.FillColor);
            var series = cut.FindComponents<ApexPointSeries<TimeSeriesModel>>();
            Assert.Equal(0, series[0].Instance.Stroke.DashSpace);
            Assert.Equal(5, series[1].Instance.Stroke.DashSpace);
        });
        forecast.HasForecastableActivity = false;
        handler.Complete(forecast);
        cut.WaitForAssertion(() =>
        {
            Assert.Contains("No recurring activity", cut.Markup);
            Assert.Empty(cut.FindAll(".cash-flow-forecast-card__chart"));
        });
    }

    [Fact]
    public async Task SnapshotHit_PaintsBeforeFetch_AndUnchangedResponseDoesNotWrite()
    {
        var snapshots = new Mock<ISnapshotService>();
        snapshots.Setup(service => service.GetAsync<CashFlowForecastCardSnapshot>(_key))
            .ReturnsAsync(Snapshot(Forecast(100m)));
        var handler = new DeferredForecastHandler();
        await using var context = CreateContext(snapshots, handler);

        var cut = context.Render<CashFlowForecastCard>();
        AssertPhoneAutoHeight(cut, "300px");
        await handler.Started.Task.WaitAsync(TimeSpan.FromSeconds(5), Xunit.TestContext.Current.CancellationToken);

        cut.WaitForAssertion(() =>
        {
            Assert.Contains(Money(100m), cut.Markup);
            Assert.DoesNotContain("mud-skeleton", cut.Markup);
        });
        var renderCount = cut.RenderCount;
        handler.Complete(Forecast(100m));
        cut.WaitForState(() => cut.RenderCount > renderCount);
        snapshots.Verify(
            service => service.SetAsync(It.IsAny<string>(), It.IsAny<CashFlowForecastCardSnapshot>()), Times.Never);
    }

    private static void AssertPhoneAutoHeight(IRenderedComponent<CashFlowForecastCard> cut, string height)
    {
        var card = cut.Find(".mud-card");
        Assert.Contains("fm-phone-auto-height", card.ClassList);
        Assert.Contains($"height:{height}", card.GetAttribute("style"));
    }

    [Fact]
    public async Task SnapshotMiss_ShowsSkeletonThenPersistsFreshForecast()
    {
        var snapshots = new Mock<ISnapshotService>();
        var handler = new DeferredForecastHandler();
        await using var context = CreateContext(snapshots, handler);

        var cut = context.Render<CashFlowForecastCard>();
        await handler.Started.Task.WaitAsync(TimeSpan.FromSeconds(5), Xunit.TestContext.Current.CancellationToken);
        cut.WaitForAssertion(() => Assert.Contains("mud-skeleton", cut.Markup));

        handler.Complete(Forecast(120m));
        cut.WaitForAssertion(() =>
        {
            Assert.Contains(Money(120m), cut.Markup);
            Assert.DoesNotContain("mud-skeleton", cut.Markup);
            snapshots.Verify(service => service.SetAsync(_key, It.Is<CashFlowForecastCardSnapshot>(
                snapshot => snapshot.UserId == 7
                    && snapshot.CurrencyId == 0
                    && snapshot.HorizonDays == 90
                    && snapshot.Model != null
                    && snapshot.Model.CurrentBalance == 120m)), Times.Once);
        });
    }

    [Fact]
    public async Task ChangedRefresh_ReplacesPaintedForecastAndSnapshot()
    {
        var snapshots = new Mock<ISnapshotService>();
        snapshots.Setup(service => service.GetAsync<CashFlowForecastCardSnapshot>(_key))
            .ReturnsAsync(Snapshot(Forecast(100m)));
        var handler = new DeferredForecastHandler();
        await using var context = CreateContext(snapshots, handler);

        var cut = context.Render<CashFlowForecastCard>();
        await handler.Started.Task.WaitAsync(TimeSpan.FromSeconds(5), Xunit.TestContext.Current.CancellationToken);
        cut.WaitForAssertion(() => Assert.Contains(Money(100m), cut.Markup));

        handler.Complete(Forecast(140m));
        cut.WaitForAssertion(() =>
        {
            Assert.Contains(Money(140m), cut.Markup);
            Assert.DoesNotContain(Money(100m), cut.Markup);
            snapshots.Verify(service => service.SetAsync(_key, It.Is<CashFlowForecastCardSnapshot>(
                snapshot => snapshot.Model != null
                    && snapshot.Model.CurrentBalance == 140m)), Times.Once);
        });
    }

    [Fact]
    public async Task RefreshFailure_KeepsPaintedForecast()
    {
        var snapshots = new Mock<ISnapshotService>();
        snapshots.Setup(service => service.GetAsync<CashFlowForecastCardSnapshot>(_key))
            .ReturnsAsync(Snapshot(Forecast(100m)));
        var handler = new DeferredForecastHandler();
        await using var context = CreateContext(snapshots, handler);

        var cut = context.Render<CashFlowForecastCard>();
        await handler.Started.Task.WaitAsync(TimeSpan.FromSeconds(5), Xunit.TestContext.Current.CancellationToken);
        var renderCount = cut.RenderCount;
        handler.Fail();
        cut.WaitForState(() => cut.RenderCount > renderCount);

        Assert.Contains(Money(100m), cut.Markup);
        Assert.DoesNotContain("Couldn't load forecast", cut.Markup);
        Assert.DoesNotContain("mud-skeleton", cut.Markup);
        snapshots.Verify(service => service.SetAsync(It.IsAny<string>(), It.IsAny<CashFlowForecastCardSnapshot>()), Times.Never);
    }

    [Theory]
    [InlineData(8, 0, 90)]
    [InlineData(7, 1, 90)]
    [InlineData(7, 0, 60)]
    public async Task MismatchedSnapshot_IsNotPainted_AndViewAllRemainsAccessible(int userId, int currencyId, int horizonDays)
    {
        var snapshots = new Mock<ISnapshotService>();
        snapshots.Setup(service => service.GetAsync<CashFlowForecastCardSnapshot>(_key))
            .ReturnsAsync(Snapshot(Forecast(100m), userId, currencyId, horizonDays));
        var handler = new DeferredForecastHandler();
        await using var context = CreateContext(snapshots, handler);

        var cut = context.Render<CashFlowForecastCard>();
        await handler.Started.Task.WaitAsync(TimeSpan.FromSeconds(5), Xunit.TestContext.Current.CancellationToken);
        cut.WaitForAssertion(() =>
        {
            Assert.Contains("mud-skeleton", cut.Markup);
            Assert.DoesNotContain(Money(100m), cut.Markup);
            var more = cut.Find("a[href='/CashFlowForecast']");
            Assert.Equal("View all", more.TextContent.Trim());
            Assert.Equal("See detailed cash flow forecast", more.GetAttribute("aria-label"));
            Assert.DoesNotContain("mud-icon", more.InnerHtml);
        });
        handler.Complete(Forecast(120m));
    }

    private static BunitContext CreateContext(Mock<ISnapshotService> snapshots, HttpMessageHandler handler)
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
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
        context.Services.AddSingleton<ISnapshotRefreshCoordinator>(
            new SnapshotRefreshCoordinator(snapshots.Object, NullLogger<SnapshotRefreshCoordinator>.Instance));
        context.Services.AddSingleton(new CashFlowForecastHttpClient(
            new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") }));
        return context;
    }

    private static string Money(decimal value) => $"{value:N2} PLN";

    private static CashFlowForecastCardSnapshot Snapshot(
        CashFlowForecast forecast, int userId = 7, int currencyId = 0, int horizonDays = 90) => new()
        {
            UserId = userId,
            CurrencyId = currencyId,
            HorizonDays = horizonDays,
            Model = CashFlowForecastCardModel.FromForecast(forecast, "PLN")
        };

    private static CashFlowForecast Forecast(decimal balance) => new()
    {
        UserId = 7,
        CurrencyId = 0,
        Currency = "PLN",
        AsOfDate = _asOfDate,
        HorizonDays = 90,
        HasForecastableActivity = true,
        ForecastSeries =
        [
            new TimeSeriesModel(_asOfDate, balance),
            new TimeSeriesModel(_asOfDate.AddDays(30), balance),
            new TimeSeriesModel(_asOfDate.AddDays(60), balance),
            new TimeSeriesModel(_asOfDate.AddDays(90), balance)
        ]
    };

    private sealed class DeferredForecastHandler : HttpMessageHandler
    {
        private readonly TaskCompletionSource<HttpResponseMessage> _response =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public void Complete(CashFlowForecast forecast) => _response.TrySetResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(forecast)
        });

        public void Fail() => _response.TrySetException(new HttpRequestException("Test failure"));

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Started.TrySetResult();
            return _response.Task;
        }
    }
}