using ApexCharts;
using Bunit;
using FinanceManager.Components.Features.Dashboard.Components.Cards.Assets;
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

namespace FinanceManager.Tests.Unit.Components.Features.Dashboard.Components.Cards.Assets;

[Trait("Category", "Unit")]
public class InvestmentRateCardTests
{
    private static readonly DateTime _asOf = new(2026, 7, 20, 13, 45, 30, DateTimeKind.Utc);
    private static readonly string _key = $"investment-rate-card:1:{DefaultCurrency.PLN.Id}:12";
    private readonly Mock<ISnapshotService> _snapshots = new();

    [Fact]
    public async Task VisitFetchesAllTwelveMonths_AndPersistsRenderedValuesWithExactAsOfMetadata()
    {
        var handler = new InvestmentRateHandler(_ => Response([new InvestmentRate { Salary = 100m, InvestmentsChange = 25m }]));
        await using var context = CreateContext(handler);
        var card = await RenderAndStartRefresh(context);

        Assert.Equal(12, handler.Count);
        Assert.Equal(new DateTime(2025, 8, 1, 0, 0, 0, DateTimeKind.Utc), handler.StartDates.Min());
        Assert.Equal(_asOf, handler.EndDates.Max());
        _snapshots.Verify(x => x.SetAsync(_key, It.Is<InvestmentRateCardSnapshot>(snapshot =>
            snapshot.UserId == 1 && snapshot.CurrencyId == DefaultCurrency.PLN.Id
            && snapshot.HorizonMonths == 12 && snapshot.AsOfMonth == new DateOnly(2026, 7, 1)
            && snapshot.AsOfDateTime == _asOf && snapshot.Model!.Months.Count == 12
            && snapshot.Model.Months.All(month => month.Salary == 100m && month.InvestmentsChange == 25m))), Times.Once);
    }

    [Fact]
    public async Task NewAuthenticatedMount_PaintsSnapshotBeforeFreshRequestsAndRefreshesAgainOnNextVisit()
    {
        var asOfMonth = new DateOnly(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
        StoredForMonth(25m, asOfMonth);
        var firstHandler = new InvestmentRateHandler(_ => Response([new InvestmentRate { Salary = 100m, InvestmentsChange = 25m }]), defer: true);
        await using var firstContext = CreateContext(firstHandler);
        var first = RenderAuthenticated(firstContext);
        first.WaitForAssertion(() => Assert.Equal(12, firstHandler.PendingCount));
        Assert.Equal(25m, first.Instance.MonthlyInvestmentRates[^1].InvestmentsChange);

        firstHandler.CompleteAll();
        first.WaitForAssertion(() => Assert.False(first.Instance.IsLoading));
        await firstContext.DisposeComponentsAsync();

        var secondHandler = new InvestmentRateHandler(_ => Response([new InvestmentRate { Salary = 100m, InvestmentsChange = 25m }]));
        await using var secondContext = CreateContext(secondHandler);
        var second = RenderAuthenticated(secondContext);
        second.WaitForAssertion(() => Assert.False(second.Instance.IsLoading));
        Assert.Equal(12, secondHandler.Count);
    }

    [Fact]
    public async Task EqualRenderedMonths_DoNotWriteWhenCaptureClockAndDecimalScaleDiffer()
    {
        Stored(25.000m);
        var handler = new InvestmentRateHandler(_ => Response([new InvestmentRate { Salary = 100.00m, InvestmentsChange = 25.00m }]), defer: true);
        await using var context = CreateContext(handler);
        var card = Render(context);
        Authenticate(context);
        card.Instance.AsOfDate = _asOf;
        var refresh = card.InvokeAsync(() => card.Instance.LoadInvestmentRatesAsync());
        card.WaitForAssertion(() => Assert.Equal(12, handler.PendingCount));
        var paintedChart = card.FindComponent<ApexChart<InvestmentRateCard.MonthBar>>().Instance;

        handler.CompleteAll();
        await refresh;

        Assert.Equal(12, handler.Count);
        Assert.Same(paintedChart, card.FindComponent<ApexChart<InvestmentRateCard.MonthBar>>().Instance);
        _snapshots.Verify(x => x.SetAsync(It.IsAny<string>(), It.IsAny<InvestmentRateCardSnapshot>()), Times.Never);
        Assert.Equal(0.25m, card.Instance.CurrentMonthPercentage);
    }

    [Fact]
    public async Task ChangedRenderedMonth_WritesSnapshot()
    {
        Stored(25m);
        var handler = new InvestmentRateHandler(request =>
            ParseStart(request).Month == 7
                ? Response([new InvestmentRate { Salary = 100m, InvestmentsChange = 30m }])
                : Response([new InvestmentRate { Salary = 100m, InvestmentsChange = 25m }]));
        await using var context = CreateContext(handler);
        var card = await RenderAndStartRefresh(context);

        _snapshots.Verify(x => x.SetAsync(_key, It.Is<InvestmentRateCardSnapshot>(snapshot =>
            snapshot.Model!.Months[snapshot.Model.Months.Count - 1].InvestmentsChange == 30m)), Times.Once);
    }

    [Fact]
    public async Task SuccessfulEmptyMonthsBecomeZeroSlotsAndReplaceSnapshot()
    {
        Stored(25m);
        var handler = new InvestmentRateHandler(_ => Response([]));
        await using var context = CreateContext(handler);
        var card = await RenderAndStartRefresh(context);

        Assert.Equal(12, handler.Count);
        Assert.All(card.Instance.MonthlyInvestmentRates, rate =>
        {
            Assert.Equal(0m, rate.Salary);
            Assert.Equal(0m, rate.InvestmentsChange);
        });
        _snapshots.Verify(x => x.SetAsync(_key, It.Is<InvestmentRateCardSnapshot>(snapshot =>
            snapshot.Model!.Months.All(month => month.Salary == 0m && month.InvestmentsChange == 0m))), Times.Once);
    }

    [Fact]
    public async Task FailedMonthPreservesPaintedSnapshotAndDoesNotWritePartialValues()
    {
        Stored(25m);
        var handler = new InvestmentRateHandler((_, index) => index == 0
            ? new HttpResponseMessage(HttpStatusCode.InternalServerError)
            : Response([new InvestmentRate { Salary = 100m, InvestmentsChange = 30m }]));
        await using var context = CreateContext(handler);
        var card = await RenderAndStartRefresh(context);

        Assert.Equal(12, handler.Count);
        Assert.Equal(25m, card.Instance.MonthlyInvestmentRates[^1].InvestmentsChange);
        _snapshots.Verify(x => x.SetAsync(It.IsAny<string>(), It.IsAny<InvestmentRateCardSnapshot>()), Times.Never);
    }

    [Theory]
    [InlineData("user")]
    [InlineData("currency")]
    [InlineData("horizon")]
    [InlineData("as-of-month")]
    [InlineData("null-month")]
    [InlineData("schema")]
    public async Task InvalidSnapshotScopeOrMonth_IsNotPainted(string invalidField)
    {
        var snapshot = SnapshotForMonth(new DateOnly(2026, 7, 1), 99m);
        switch (invalidField)
        {
            case "user": snapshot.UserId = 2; break;
            case "currency": snapshot.CurrencyId = 2; break;
            case "horizon": snapshot.HorizonMonths = 11; break;
            case "as-of-month": snapshot.AsOfMonth = new DateOnly(2026, 6, 1); break;
            case "null-month":
                var months = snapshot.Model!.Months.ToArray();
                months[0] = null!;
                snapshot.Model = new InvestmentRateCardModel(months);
                break;
            case "schema": snapshot.SchemaVersion++; break;
        }
        _snapshots.Setup(x => x.GetAsync<InvestmentRateCardSnapshot>(_key)).ReturnsAsync(snapshot);
        var handler = new InvestmentRateHandler(_ => Response([new InvestmentRate { Salary = 100m, InvestmentsChange = 30m }]), defer: true);
        await using var context = CreateContext(handler);
        var card = Render(context);
        Authenticate(context);
        card.Instance.AsOfDate = _asOf;
        var refresh = card.InvokeAsync(() => card.Instance.LoadInvestmentRatesAsync());
        card.WaitForAssertion(() => Assert.Equal(12, handler.PendingCount));

        Assert.Empty(card.Instance.MonthlyInvestmentRates);
        handler.CompleteAll();
        await refresh;

        Assert.Equal(30m, card.Instance.MonthlyInvestmentRates[^1].InvestmentsChange);
    }

    [Fact]
    public async Task SameScopeSnapshotReadFailureAndFreshFailureRetainsMountedValues()
    {
        var handler = new InvestmentRateHandler((_, index) => index < 12
            ? Response([new InvestmentRate { Salary = 100m, InvestmentsChange = 25m }])
            : new HttpResponseMessage(HttpStatusCode.InternalServerError));
        await using var context = CreateContext(handler);
        var card = await RenderAndStartRefresh(context);
        _snapshots.SetupSequence(x => x.GetAsync<InvestmentRateCardSnapshot>(_key))
            .ReturnsAsync((InvestmentRateCardSnapshot?)null)
            .ThrowsAsync(new InvalidOperationException("storage unavailable"));

        await card.InvokeAsync(() => card.Instance.LoadInvestmentRatesAsync());

        Assert.Equal(25m, card.Instance.MonthlyInvestmentRates[^1].InvestmentsChange);
        Assert.DoesNotContain("Investment rate unavailable", card.Markup);
        _snapshots.Verify(x => x.SetAsync(It.IsAny<string>(), It.IsAny<InvestmentRateCardSnapshot>()), Times.Once);
    }

    [Fact]
    public async Task NewUserSettingsFailure_ClearsPreviouslyMountedUserData()
    {
        var handler = new InvestmentRateHandler(_ => Response([new InvestmentRate { Salary = 100m, InvestmentsChange = 25m }]));
        await using var context = CreateContext(handler);
        var card = await RenderAndStartRefresh(context);
        Mock.Get(context.Services.GetRequiredService<ILoginService>())
            .Setup(x => x.GetLoggedUser())
            .ReturnsAsync(new UserSession { UserId = 2, UserName = "other", Password = "", UserRole = UserRole.User });
        Mock.Get(context.Services.GetRequiredService<ISettingsService>())
            .Setup(x => x.GetCurrencyAsync())
            .ThrowsAsync(new InvalidOperationException("settings unavailable"));

        await card.InvokeAsync(() => card.Instance.LoadInvestmentRatesAsync());

        Assert.Empty(card.Instance.MonthlyInvestmentRates);
        card.WaitForAssertion(() => Assert.Contains("Investment rate unavailable", card.Markup));
    }

    [Fact]
    public async Task CurrencyChange_ClearsOldViewBeforeSnapshotReadCompletes()
    {
        var handler = new InvestmentRateHandler((_, index) => index < 12
            ? Response([new InvestmentRate { Salary = 100m, InvestmentsChange = 25m }])
            : new HttpResponseMessage(HttpStatusCode.InternalServerError));
        await using var context = CreateContext(handler);
        var card = await RenderAndStartRefresh(context);
        var usdKey = $"investment-rate-card:1:{DefaultCurrency.USD.Id}:12";
        var readStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseRead = new TaskCompletionSource<InvestmentRateCardSnapshot?>(TaskCreationOptions.RunContinuationsAsynchronously);
        _snapshots.Setup(x => x.GetAsync<InvestmentRateCardSnapshot>(usdKey))
            .Returns(async () =>
            {
                readStarted.TrySetResult();
                return await releaseRead.Task;
            });
        Mock.Get(context.Services.GetRequiredService<ISettingsService>())
            .Setup(x => x.GetCurrencyAsync())
            .ReturnsAsync(DefaultCurrency.USD);
        var refresh = card.InvokeAsync(() => card.Instance.LoadInvestmentRatesAsync());
        await readStarted.Task;

        Assert.Empty(card.Instance.MonthlyInvestmentRates);
        releaseRead.SetResult(null);
        await refresh;

        Assert.Empty(card.Instance.MonthlyInvestmentRates);
        card.WaitForAssertion(() => Assert.Contains("Investment rate unavailable", card.Markup));
    }

    [Fact]
    public async Task SnapshotWriteFailure_LeavesFreshValuesVisible()
    {
        var handler = new InvestmentRateHandler(_ => Response([new InvestmentRate { Salary = 100m, InvestmentsChange = 30m }]));
        await using var context = CreateContext(handler);
        _snapshots.Setup(x => x.SetAsync(_key, It.IsAny<InvestmentRateCardSnapshot>()))
            .ThrowsAsync(new InvalidOperationException("storage unavailable"));
        var card = await RenderAndStartRefresh(context);

        Assert.Equal(30m, card.Instance.MonthlyInvestmentRates[^1].InvestmentsChange);
        Assert.DoesNotContain("Investment rate unavailable", card.Markup);
        _snapshots.Verify(x => x.SetAsync(_key, It.IsAny<InvestmentRateCardSnapshot>()), Times.Once);
    }

    [Fact]
    public async Task SupersededMonthBatch_CannotPaintOrWriteAfterNewerBatch()
    {
        var handler = new InvestmentRateHandler((_, index) => Response([
            new InvestmentRate { Salary = 100m, InvestmentsChange = index < 12 ? 25m : 40m },
        ]), defer: true);
        await using var context = CreateContext(handler);
        var card = Render(context);
        Authenticate(context);
        card.Instance.AsOfDate = _asOf;
        var olderRefresh = card.InvokeAsync(() => card.Instance.LoadInvestmentRatesAsync());
        card.WaitForAssertion(() => Assert.Equal(12, handler.PendingCount));
        var newerRefresh = card.InvokeAsync(() => card.Instance.LoadInvestmentRatesAsync());
        card.WaitForAssertion(() => Assert.Equal(24, handler.PendingCount));

        handler.CompleteRange(12, 24);
        await newerRefresh;
        Assert.Equal(40m, card.Instance.MonthlyInvestmentRates[^1].InvestmentsChange);
        _snapshots.Verify(x => x.SetAsync(It.IsAny<string>(), It.IsAny<InvestmentRateCardSnapshot>()), Times.Once);

        handler.CompleteRange(0, 12);
        await olderRefresh;

        Assert.Equal(40m, card.Instance.MonthlyInvestmentRates[^1].InvestmentsChange);
        _snapshots.Verify(x => x.SetAsync(It.IsAny<string>(), It.IsAny<InvestmentRateCardSnapshot>()), Times.Once);
    }

    [Fact]
    public async Task NullBodyIsFailureRatherThanAnEmptyMonth()
    {
        var handler = new InvestmentRateHandler((_, index) => index == 0
            ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("null", System.Text.Encoding.UTF8, "application/json") }
            : Response([]));
        await using var context = CreateContext(handler);
        var card = await RenderAndStartRefresh(context);

        card.WaitForAssertion(() => Assert.Contains("Investment rate unavailable", card.Markup));
        Assert.Empty(card.Instance.MonthlyInvestmentRates);
        _snapshots.Verify(x => x.SetAsync(It.IsAny<string>(), It.IsAny<InvestmentRateCardSnapshot>()), Times.Never);
    }

    [Fact]
    public async Task SelectedMonthSurvivesSnapshotToFreshReconcile()
    {
        Stored(25m);
        var handler = new InvestmentRateHandler(_ => Response([new InvestmentRate { Salary = 100m, InvestmentsChange = 30m }]), defer: true);
        await using var context = CreateContext(handler);
        var card = Render(context);
        Authenticate(context);
        card.Instance.AsOfDate = _asOf;
        var refresh = card.InvokeAsync(() => card.Instance.LoadInvestmentRatesAsync());
        card.WaitForAssertion(() => Assert.Equal(12, handler.PendingCount));
        card.WaitForAssertion(() => Assert.Equal(12, card.Instance.MonthlyInvestmentRates.Count));
        Assert.True(card.Instance.SelectMonth(4));
        var selectedMonth = (card.Instance.SelectedMonthRate!.Start.Year, card.Instance.SelectedMonthRate.Start.Month);
        card.Instance.BuildDerivedState();
        card.Render();
        var paintedChart = card.FindComponent<ApexChart<InvestmentRateCard.MonthBar>>().Instance;

        handler.CompleteAll();
        await refresh;

        Assert.Equal(selectedMonth, (card.Instance.SelectedMonthRate!.Start.Year, card.Instance.SelectedMonthRate.Start.Month));
        Assert.NotSame(paintedChart, card.FindComponent<ApexChart<InvestmentRateCard.MonthBar>>().Instance);
        Assert.Equal(30m, card.Instance.MonthlyInvestmentRates[^1].InvestmentsChange);
    }

    [Fact]
    public async Task DisposeDuringRefresh_PreventsLatePaintAndSnapshotWrite()
    {
        var handler = new InvestmentRateHandler(_ => Response([new InvestmentRate { Salary = 100m, InvestmentsChange = 25m }]), defer: true);
        await using var context = CreateContext(handler);
        var card = Render(context);
        Authenticate(context);
        card.Instance.AsOfDate = _asOf;
        var refresh = card.InvokeAsync(() => card.Instance.LoadInvestmentRatesAsync());
        card.WaitForAssertion(() => Assert.Equal(12, handler.PendingCount));

        await context.DisposeComponentsAsync();
        handler.CompleteAll();
        await refresh;

        _snapshots.Verify(x => x.SetAsync(It.IsAny<string>(), It.IsAny<InvestmentRateCardSnapshot>()), Times.Never);
    }

    [Fact]
    public async Task BuildDerivedState_PreservesSalarylessCurrentMonthAndAllYtdInvestments()
    {
        await using var context = CreateContext(new InvestmentRateHandler(_ => Response([])));
        var card = Render(context);
        card.Instance.AsOfDate = new DateTime(2026, 7, 20, 0, 0, 0, DateTimeKind.Utc);
        card.Instance.MonthlyInvestmentRates =
        [
            new InvestmentRate { Start = new DateTime(2026, 6, 1), Salary = 9456.88m, InvestmentsChange = 4000m },
            new InvestmentRate { Start = new DateTime(2026, 7, 1), Salary = 0m, InvestmentsChange = 5523.17m },
        ];

        card.Instance.BuildDerivedState();

        Assert.Null(card.Instance.CurrentMonthPercentage);
        Assert.Equal(4000m / 9456.88m, card.Instance.YtdAveragePercentage);
        Assert.Equal((4000m + 5523.17m) / 7m * 12m, card.Instance.EndOfYearProjection);
        Assert.Null(card.Instance.Series[1].Percentage);
        Assert.Equal(4000m / 9456.88m * 100m, card.Instance.Series[0].Percentage);
    }

    [Fact]
    public async Task SelectMonth_UpdatesSelectionAndIgnoresPlaceholderBars()
    {
        await using var context = CreateContext(new InvestmentRateHandler(_ => Response([])));
        var card = Render(context);
        var january = new InvestmentRate { Start = new DateTime(2026, 1, 1) };
        var february = new InvestmentRate { Start = new DateTime(2026, 2, 1) };
        card.Instance.MonthlyInvestmentRates = [january, february];

        Assert.True(card.Instance.SelectMonth(0));
        Assert.Same(january, card.Instance.SelectedMonthRate);
        Assert.False(card.Instance.SelectMonth(2));
        Assert.Same(january, card.Instance.SelectedMonthRate);
    }

    [Fact]
    public async Task BuildDerivedState_CurrentMonthWithSalary_ReportsRate()
    {
        await using var context = CreateContext(new InvestmentRateHandler(_ => Response([])));
        var card = Render(context);
        card.Instance.AsOfDate = new DateTime(2026, 7, 20, 0, 0, 0, DateTimeKind.Utc);
        var july = new InvestmentRate { Start = new DateTime(2026, 7, 1), Salary = 10_000m, InvestmentsChange = 5000m };
        card.Instance.MonthlyInvestmentRates = [july];

        card.Instance.BuildDerivedState();

        Assert.Equal(0.5m, card.Instance.CurrentMonthPercentage);
        Assert.Equal(0.5m, card.Instance.YtdAveragePercentage);
        Assert.Equal(50m, card.Instance.Series[0].Percentage);
    }

    private void Stored(decimal investmentChange)
    {
        StoredForMonth(investmentChange, new DateOnly(2026, 7, 1));
    }

    private void StoredForMonth(decimal investmentChange, DateOnly asOfMonth)
    {
        _snapshots.Setup(x => x.GetAsync<InvestmentRateCardSnapshot>(_key)).ReturnsAsync(SnapshotForMonth(asOfMonth, investmentChange));
    }

    private static InvestmentRateCardSnapshot SnapshotForMonth(DateOnly asOfMonth, decimal investmentChange)
    {
        var firstMonth = asOfMonth.AddMonths(-11);
        var model = new InvestmentRateCardModel(Enumerable.Range(0, 12)
            .Select(index => new InvestmentRateMonthModel(firstMonth.AddMonths(index), 100m, investmentChange)).ToArray());
        return new InvestmentRateCardSnapshot
        {
            SchemaVersion = FinanceManager.Components.Shared.Models.SnapshotBase.CurrentSchemaVersion,
            UserId = 1,
            CurrencyId = DefaultCurrency.PLN.Id,
            HorizonMonths = 12,
            AsOfMonth = asOfMonth,
            AsOfDateTime = new DateTime(asOfMonth.Year, asOfMonth.Month, 1, 0, 0, 0, DateTimeKind.Unspecified),
            Model = model,
        };
    }

    private async Task<IRenderedComponent<InvestmentRateCard>> RenderAndStartRefresh(BunitContext context)
    {
        var card = Render(context);
        Authenticate(context);
        card.Instance.AsOfDate = _asOf;
        await card.InvokeAsync(() => card.Instance.LoadInvestmentRatesAsync());
        Assert.Equal(12, context.Services.GetRequiredService<InvestmentRateHandler>().Count);
        return card;
    }

    private BunitContext CreateContext(InvestmentRateHandler handler)
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddLogging();
        context.Services.AddMudServices();
        var settings = new Mock<ISettingsService>();
        settings.Setup(x => x.GetCurrencyAsync()).ReturnsAsync(DefaultCurrency.PLN);
        var login = new Mock<ILoginService>();
        login.Setup(x => x.GetLoggedUser()).ReturnsAsync(() => new UserSession { UserId = 1, UserName = "tester", Password = "", UserRole = UserRole.User });
        context.Services.AddSingleton(settings.Object);
        context.Services.AddSingleton(login.Object);
        context.Services.AddSingleton<ISnapshotRefreshCoordinator>(new SnapshotRefreshCoordinator(_snapshots.Object, NullLogger<SnapshotRefreshCoordinator>.Instance));
        context.Services.AddSingleton(handler);
        context.Services.AddSingleton(new MoneyFlowHttpClient(new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") }));
        return context;
    }

    private static IRenderedComponent<InvestmentRateCard> Render(BunitContext context)
    {
        var login = Mock.Get(context.Services.GetRequiredService<ILoginService>());
        login.Setup(x => x.GetLoggedUser()).ReturnsAsync((UserSession?)null);
        return context.Render<InvestmentRateCard>();
    }

    private static IRenderedComponent<InvestmentRateCard> RenderAuthenticated(BunitContext context) =>
        context.Render<InvestmentRateCard>();

    private static void Authenticate(BunitContext context) =>
        Mock.Get(context.Services.GetRequiredService<ILoginService>())
            .Setup(x => x.GetLoggedUser())
            .ReturnsAsync(() => new UserSession { UserId = 1, UserName = "tester", Password = "", UserRole = UserRole.User });

    private static HttpResponseMessage Response(List<InvestmentRate> rates) => new(HttpStatusCode.OK) { Content = JsonContent.Create(rates) };

    private static DateTime ParseStart(HttpRequestMessage request)
    {
        var encoded = request.RequestUri!.Query.Split('&').Single(part => part.StartsWith("start=", StringComparison.Ordinal))[6..];
        return DateTime.Parse(Uri.UnescapeDataString(encoded), null, System.Globalization.DateTimeStyles.RoundtripKind);
    }

    private sealed class InvestmentRateHandler(
        Func<HttpRequestMessage, int, HttpResponseMessage> response,
        bool defer = false) : HttpMessageHandler
    {
        private readonly List<(TaskCompletionSource<HttpResponseMessage> Completion, HttpResponseMessage Response)> _pending = [];
        private readonly object _sync = new();
        private int _count;
        public int Count => Volatile.Read(ref _count);
        public int PendingCount { get { lock (_sync) return _pending.Count; } }
        public List<DateTime> StartDates { get; } = [];
        public List<DateTime> EndDates { get; } = [];

        public InvestmentRateHandler(Func<HttpRequestMessage, HttpResponseMessage> response, bool defer = false)
            : this((request, _) => response(request), defer) { }

        public void CompleteAll()
            => CompleteRange(0, Count);

        public void CompleteRange(int startIndex, int endIndex)
        {
            lock (_sync)
                foreach (var pending in _pending.Skip(startIndex).Take(endIndex - startIndex))
                    pending.Completion.TrySetResult(pending.Response);
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var index = Interlocked.Increment(ref _count) - 1;
            var responseMessage = response(request, index);
            var end = request.RequestUri!.Query.Split('&').Single(part => part.StartsWith("end=", StringComparison.Ordinal))[4..];
            lock (_sync)
            {
                StartDates.Add(ParseStart(request));
                EndDates.Add(DateTime.Parse(Uri.UnescapeDataString(end), null, System.Globalization.DateTimeStyles.RoundtripKind));
            }
            if (!defer) return Task.FromResult(responseMessage);

            var completion = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
            lock (_sync) _pending.Add((completion, responseMessage));
            return completion.Task;
        }
    }
}