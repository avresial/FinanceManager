using Bunit;
using FinanceManager.Components.Features.Dashboard.Components.Cards.Assets;
using FinanceManager.Components.Features.Dashboard.Models;
using FinanceManager.Components.Features.MoneyFlow.HttpClients;
using FinanceManager.Components.Shared.Services;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Investments.Dtos;
using FinanceManager.Domain.Identity.Entities;
using FinanceManager.Domain.Identity.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MudBlazor.Services;
using System.Globalization;
using System.Net;
using System.Net.Http.Json;

namespace FinanceManager.Tests.Unit.Components.Features.Dashboard.Components.Cards.Assets;

[Trait("Category", "Unit")]
public class FeeDragCardTests
{
    private static readonly DateTime _asOf = new(2026, 9, 15, 0, 0, 0, DateTimeKind.Utc);
    private static readonly string _key = $"fee-drag:1:{DefaultCurrency.PLN.Id}";
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(5);
    private readonly Mock<ISnapshotService> _snapshots = new();

    [Fact]
    public async Task PopulatedCard_RendersAnnualFeeMissingTerAndFixedHorizons()
    {
        await using var context = CreateContext(new FeeDragHandler());
        var cut = context.Render<FeeDragCard>();
        cut.Instance.Analysis = new FeeDragAnalysisResult
        {
            TotalHoldingsValue = 100_000m,
            MissingTerHoldingsValue = 20_000m,
            MissingTerCount = 1,
            TotalHoldingsCount = 3,
            AnnualFeeCost = 250m,
            WeightedExpenseRatio = 0.0025m,
        };
        cut.Render();

        Assert.Contains(250m.ToString("N2", CultureInfo.CurrentCulture), cut.Find("[data-testid='fee-drag-annual-fee']").TextContent);
        Assert.Contains("Expense ratio not set", cut.Find("[data-testid='fee-drag-missing-ter']").TextContent);
        Assert.NotNull(cut.Find("[data-testid='fee-drag-projection-10']"));
        Assert.NotNull(cut.Find("[data-testid='fee-drag-projection-20']"));
        Assert.NotNull(cut.Find("[data-testid='fee-drag-projection-30']"));
        Assert.Contains($"Avg TER {FeeDragCard.FormatPercentage(0.0025m)}", cut.Markup);
    }

    [Fact]
    public async Task ReturnRate_IsClampedAndRecalculatesProjections()
    {
        await using var context = CreateContext(new FeeDragHandler());
        var cut = context.Render<FeeDragCard>();
        cut.Instance.Analysis = new FeeDragAnalysisResult
        {
            TotalHoldingsValue = 100_000m,
            TotalHoldingsCount = 1,
            WeightedExpenseRatio = 0.005m,
        };
        cut.Instance.OnReturnRateChanged(0.02m);
        Assert.Equal(0m, cut.Instance.Analysis!.AssumedAnnualReturnRate);
        var initial = cut.Instance.ComputedProjections[0].CumulativeFeeCost;

        cut.Instance.OnReturnRateChanged(0.50m);
        Assert.Equal(0.10m, cut.Instance.AssumedAnnualReturnRate);
        Assert.NotEqual(initial, cut.Instance.ComputedProjections[0].CumulativeFeeCost);

        cut.Instance.OnReturnRateChanged(-0.50m);
        Assert.Equal(-0.10m, cut.Instance.AssumedAnnualReturnRate);
    }

    [Fact]
    public async Task EmptyCard_RendersEmptyState()
    {
        await using var context = CreateContext(new FeeDragHandler());
        var cut = context.Render<FeeDragCard>();

        Assert.NotNull(cut.Find("[data-testid='fee-drag-empty']"));
        Assert.Contains("No ETF holdings found", cut.Markup);
    }

    [Fact]
    public async Task AsOfDateChange_ReloadsOnceAndIgnoresStaleResponse()
    {
        var secondDate = _asOf.AddDays(1);
        var handler = new FeeDragHandler();
        await using var context = CreateContext(handler, User());

        var cut = Render(context);
        cut.WaitForAssertion(() => Assert.Equal(1, handler.Count), _timeout);

        cut.Render(parameters => parameters.Add(component => component.AsOfDate, secondDate));
        cut.WaitForAssertion(() => Assert.Equal(2, handler.Count), _timeout);

        handler.Complete(1, Analysis(200m));
        cut.WaitForAssertion(() => Assert.Equal(200m, cut.Instance.Analysis?.AnnualFeeCost), _timeout);

        cut.Render(parameters => parameters.Add(component => component.AsOfDate, secondDate));
        Assert.Equal(2, handler.Count);

        handler.Complete(0, Analysis(100m));
        await Drain(cut);

        Assert.Equal(200m, cut.Instance.Analysis?.AnnualFeeCost);
        Assert.Empty(cut.FindAll("[data-testid='fee-drag-error']"));
        _snapshots.Verify(x => x.SetAsync(_key, It.Is<FeeDragSnapshot>(s => s.Analysis.AnnualFeeCost == 100m)), Times.Never);
        _snapshots.Verify(x => x.SetAsync(_key, It.Is<FeeDragSnapshot>(s => s.Analysis.AnnualFeeCost == 200m && s.AsOfDate == secondDate)), Times.Once);
    }

    [Fact]
    public async Task HydratesBeforeFetchCompletes_AndFetchesOnEveryVisit()
    {
        Stored(100m);
        var handler = new FeeDragHandler();
        await using var context = CreateContext(handler, User());

        var first = Render(context);
        first.WaitForAssertion(() => Assert.Equal(100m, first.Instance.Analysis?.AnnualFeeCost), _timeout);
        Assert.Equal(1, handler.Count);
        Assert.Contains("fee-drag-annual-fee", first.Markup);
        Assert.Empty(first.FindAll("[data-testid='fee-drag-loading']"));
        Assert.Contains("PLN", first.Find(".fee-drag-card__hero-currency").TextContent);

        handler.Complete(0, Analysis(100m));
        await Drain(first);
        first.Dispose();

        var second = Render(context);
        second.WaitForAssertion(() => Assert.Equal(2, handler.Count), _timeout);
        Assert.Equal(100m, second.Instance.Analysis?.AnnualFeeCost);
        handler.Complete(1, Analysis(100m));
        await Drain(second);
    }

    [Fact]
    public async Task EqualAnalysis_DoesNotWrite_EvenWithDifferentServerProjectionsAndRate()
    {
        Stored(100m);
        var handler = new FeeDragHandler();
        await using var context = CreateContext(handler, User());
        var cut = Render(context);
        cut.WaitForAssertion(() => Assert.Equal(1, handler.Count), _timeout);

        var fresh = Analysis(100m);
        fresh.AssumedAnnualReturnRate = 0.07m;
        fresh.Projections = [new FeeDragProjection { Years = 10, CumulativeFeeCost = 5m }];
        handler.Complete(0, fresh);
        await Drain(cut);

        _snapshots.Verify(x => x.SetAsync(It.IsAny<string>(), It.IsAny<FeeDragSnapshot>()), Times.Never);
    }

    [Fact]
    public async Task ChangedAnalysis_RepaintsAndWritesWithKeyAndAsOfDate()
    {
        Stored(100m);
        var handler = new FeeDragHandler();
        await using var context = CreateContext(handler, User());
        var cut = Render(context);
        cut.WaitForAssertion(() => Assert.Equal(1, handler.Count), _timeout);

        var fresh = Analysis(300m);
        fresh.Projections = [new FeeDragProjection { Years = 10 }];
        fresh.AssumedAnnualReturnRate = 0.07m;
        handler.Complete(0, fresh);
        cut.WaitForAssertion(() => Assert.Equal(300m, cut.Instance.Analysis?.AnnualFeeCost), _timeout);
        await Drain(cut);

        _snapshots.Verify(x => x.SetAsync(_key, It.Is<FeeDragSnapshot>(s =>
            s.UserId == 1 && s.CurrencyId == DefaultCurrency.PLN.Id && s.AsOfDate == _asOf
            && s.Analysis.AnnualFeeCost == 300m && s.Analysis.Projections.Count == 0
            && s.Analysis.AssumedAnnualReturnRate == 0m)), Times.Once);
    }

    [Fact]
    public async Task SliderChange_NeverWritesOrFetches_AndLaterEqualRefreshIsUnchanged()
    {
        Stored(100m);
        var handler = new FeeDragHandler();
        await using var context = CreateContext(handler, User());
        var cut = Render(context);
        cut.WaitForAssertion(() => Assert.Equal(1, handler.Count), _timeout);
        handler.Complete(0, Analysis(100m));
        await Drain(cut);

        cut.Instance.OnReturnRateChanged(0.03m);
        cut.Render();
        Assert.Equal(1, handler.Count);
        Assert.Equal(0m, cut.Instance.Analysis!.AssumedAnnualReturnRate);
        _snapshots.Verify(x => x.SetAsync(It.IsAny<string>(), It.IsAny<FeeDragSnapshot>()), Times.Never);

        var retry = cut.InvokeAsync(() => cut.Instance.LoadFeeDragDataAsync());
        cut.WaitForAssertion(() => Assert.Equal(2, handler.Count), _timeout);
        Assert.Contains("assumedAnnualReturnRate=0.07", handler.Requests[1]);
        handler.Complete(1, Analysis(100m));
        await retry;

        Assert.Equal(0.03m, cut.Instance.AssumedAnnualReturnRate);
        _snapshots.Verify(x => x.SetAsync(It.IsAny<string>(), It.IsAny<FeeDragSnapshot>()), Times.Never);
    }

    [Fact]
    public async Task NoEtfSuccess_ClearsToEmptyStateAndWrites()
    {
        Stored(100m);
        var handler = new FeeDragHandler();
        await using var context = CreateContext(handler, User());
        var cut = Render(context);
        cut.WaitForAssertion(() => Assert.Equal(1, handler.Count), _timeout);

        handler.Complete(0, new FeeDragAnalysisResult { TotalHoldingsCount = 0 });
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid='fee-drag-empty']")), _timeout);
        await Drain(cut);

        _snapshots.Verify(x => x.SetAsync(_key, It.Is<FeeDragSnapshot>(s => s.Analysis.TotalHoldingsCount == 0)), Times.Once);
    }

    [Fact]
    public async Task NullBody_KeepsStoredSnapshot()
    {
        Stored(100m);
        var handler = new FeeDragHandler();
        await using var context = CreateContext(handler, User());
        var cut = Render(context);
        cut.WaitForAssertion(() => Assert.Equal(1, handler.Count), _timeout);

        handler.CompleteNull(0);
        await Drain(cut);

        Assert.Equal(100m, cut.Instance.Analysis?.AnnualFeeCost);
        Assert.Empty(cut.FindAll("[data-testid='fee-drag-error']"));
        _snapshots.Verify(x => x.SetAsync(It.IsAny<string>(), It.IsAny<FeeDragSnapshot>()), Times.Never);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task FailedRefresh_PreservesSnapshotOrShowsBlockingError(bool stored)
    {
        if (stored)
            Stored(100m);
        var handler = new FeeDragHandler();
        await using var context = CreateContext(handler, User());
        var cut = Render(context);
        cut.WaitForAssertion(() => Assert.Equal(1, handler.Count), _timeout);

        handler.Fail(0);
        cut.WaitForAssertion(() =>
        {
            Assert.Empty(cut.FindAll("[data-testid='fee-drag-loading']"));
            Assert.Equal(!stored, cut.FindAll("[data-testid='fee-drag-error']").Count == 1);
        }, _timeout);

        if (stored)
            Assert.Equal(100m, cut.Instance.Analysis?.AnnualFeeCost);
        _snapshots.Verify(x => x.SetAsync(It.IsAny<string>(), It.IsAny<FeeDragSnapshot>()), Times.Never);
    }

    [Theory]
    [InlineData(2, 0, 0)]
    [InlineData(1, 5, 0)]
    [InlineData(1, 0, 1)]
    public async Task SnapshotForDifferentUserCurrencyOrDay_IsNotPainted(int userId, int currencyId, int dayOffset)
    {
        _snapshots.Setup(x => x.GetAsync<FeeDragSnapshot>(_key)).ReturnsAsync(new FeeDragSnapshot
        {
            UserId = userId,
            CurrencyId = currencyId == 0 ? DefaultCurrency.PLN.Id : currencyId,
            AsOfDate = _asOf.AddDays(dayOffset),
            Analysis = Analysis(100m),
        });
        var handler = new FeeDragHandler();
        await using var context = CreateContext(handler, User());
        var cut = Render(context);
        cut.WaitForAssertion(() => Assert.Equal(1, handler.Count), _timeout);

        Assert.Null(cut.Instance.Analysis);
        Assert.NotEmpty(cut.FindAll("[data-testid='fee-drag-loading']"));
        handler.Complete(0, Analysis(200m));
        cut.WaitForAssertion(() => Assert.Equal(200m, cut.Instance.Analysis?.AnnualFeeCost), _timeout);
        await Drain(cut);
    }

    [Fact]
    public async Task StorageReadFailure_StillFetchesAndRenders()
    {
        _snapshots.Setup(x => x.GetAsync<FeeDragSnapshot>(_key)).ThrowsAsync(new InvalidOperationException());
        var handler = new FeeDragHandler();
        await using var context = CreateContext(handler, User());
        var cut = Render(context);
        cut.WaitForAssertion(() => Assert.Equal(1, handler.Count), _timeout);

        handler.Complete(0, Analysis(200m));
        cut.WaitForAssertion(() => Assert.Equal(200m, cut.Instance.Analysis?.AnnualFeeCost), _timeout);
        _snapshots.Verify(x => x.RemoveAsync(_key), Times.Once);
        await Drain(cut);
    }

    [Fact]
    public async Task StorageWriteFailure_KeepsFreshAnalysis()
    {
        _snapshots.Setup(x => x.SetAsync(It.IsAny<string>(), It.IsAny<FeeDragSnapshot>())).ThrowsAsync(new InvalidOperationException());
        var handler = new FeeDragHandler();
        await using var context = CreateContext(handler, User());
        var cut = Render(context);
        cut.WaitForAssertion(() => Assert.Equal(1, handler.Count), _timeout);

        handler.Complete(0, Analysis(200m));
        cut.WaitForAssertion(() => Assert.Equal(200m, cut.Instance.Analysis?.AnnualFeeCost), _timeout);
        await Drain(cut);
        Assert.Empty(cut.FindAll("[data-testid='fee-drag-error']"));
    }

    [Fact]
    public async Task DisposeMidFlight_DoesNotWrite()
    {
        var handler = new FeeDragHandler();
        await using var context = CreateContext(handler, User());
        var cut = Render(context);
        cut.WaitForAssertion(() => Assert.Equal(1, handler.Count), _timeout);

        cut.Instance.Dispose();
        handler.Complete(0, Analysis(200m));
        await Task.Delay(100, Xunit.TestContext.Current.CancellationToken);

        _snapshots.Verify(x => x.SetAsync(It.IsAny<string>(), It.IsAny<FeeDragSnapshot>()), Times.Never);
    }

    private void Stored(decimal annualFeeCost) =>
        _snapshots.Setup(x => x.GetAsync<FeeDragSnapshot>(_key)).ReturnsAsync(new FeeDragSnapshot
        {
            UserId = 1,
            CurrencyId = DefaultCurrency.PLN.Id,
            AsOfDate = _asOf.AddHours(5),
            Analysis = Analysis(annualFeeCost),
            FetchedAtUtc = _asOf.AddYears(-1),
        });

    private static FeeDragAnalysisResult Analysis(decimal annualFeeCost) => new()
    {
        TotalHoldingsValue = 10_000m,
        AnnualFeeCost = annualFeeCost,
        WeightedExpenseRatio = 0.002m,
        TotalHoldingsCount = 1,
        Holdings = [new FeeDragHolding { ListingId = 1, Ticker = "VWCE", Name = "World", Value = 10_000m, ExpenseRatio = 0.002m, AnnualFeeCost = annualFeeCost }],
    };

    private static UserSession User() => new()
    {
        UserId = 1,
        UserName = "tester",
        Password = string.Empty,
        UserRole = UserRole.User,
    };

    private static IRenderedComponent<FeeDragCard> Render(BunitContext context) =>
        context.Render<FeeDragCard>(parameters => parameters.Add(component => component.AsOfDate, _asOf));

    private static Task Drain(IRenderedComponent<FeeDragCard> cut) => cut.InvokeAsync(async () => await Task.Delay(50, Xunit.TestContext.Current.CancellationToken));

    private BunitContext CreateContext(HttpMessageHandler handler, UserSession? user = null)
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddLogging();
        context.Services.AddMudServices();

        var settings = new Mock<ISettingsService>();
        settings.Setup(service => service.GetCurrencyAsync()).ReturnsAsync(DefaultCurrency.PLN);
        context.Services.AddSingleton(settings.Object);

        var login = new Mock<ILoginService>();
        login.Setup(service => service.GetLoggedUser()).ReturnsAsync(user);
        context.Services.AddSingleton(login.Object);
        context.Services.AddSingleton<ISnapshotRefreshCoordinator>(
            new SnapshotRefreshCoordinator(_snapshots.Object, NullLogger<SnapshotRefreshCoordinator>.Instance));
        context.Services.AddSingleton(new AssetsHttpClient(new HttpClient(handler)
        {
            BaseAddress = new Uri("http://localhost/"),
        }));

        return context;
    }

    private sealed class FeeDragHandler : HttpMessageHandler
    {
        private readonly object _lock = new();
        private readonly List<TaskCompletionSource<HttpResponseMessage>> _responses = [];
        private readonly List<string> _requests = [];

        public int Count
        {
            get { lock (_lock) return _responses.Count; }
        }

        public IReadOnlyList<string> Requests
        {
            get { lock (_lock) return [.. _requests]; }
        }

        public void Complete(int index, FeeDragAnalysisResult analysis) =>
            Respond(index, new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(analysis) });

        public void CompleteNull(int index) =>
            Respond(index, new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("null", System.Text.Encoding.UTF8, "application/json") });

        public void Fail(int index) => Respond(index, new HttpResponseMessage(HttpStatusCode.InternalServerError));

        private void Respond(int index, HttpResponseMessage response)
        {
            TaskCompletionSource<HttpResponseMessage> source;
            lock (_lock) source = _responses[index];
            source.TrySetResult(response);
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
            lock (_lock)
            {
                _responses.Add(response);
                _requests.Add(request.RequestUri!.ToString());
            }

            return response.Task;
        }
    }
}