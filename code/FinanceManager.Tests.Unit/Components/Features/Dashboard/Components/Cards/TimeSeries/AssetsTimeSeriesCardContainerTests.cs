using Blazored.LocalStorage;
using Bunit;
using FinanceManager.Components.Features.Dashboard.Components.Cards.TimeSeries;
using FinanceManager.Components.Features.Dashboard.Models;
using FinanceManager.Components.Features.Dashboard.Services;
using FinanceManager.Components.Features.MoneyFlow.HttpClients;
using FinanceManager.Components.Shared.Components.Charts;
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

namespace FinanceManager.Tests.Unit.Components.Features.Dashboard.Components.Cards.TimeSeries;

[Trait("Category", "Unit")]
public class AssetsTimeSeriesCardContainerTests
{
    private static readonly DateTime _start = new(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime _end = _start.AddDays(30).AddHours(12);
    private static readonly string _key = $"assets-timeseries:1:{DefaultCurrency.PLN.Id}";
    private readonly Mock<ISnapshotService> _snapshots = new();

    [Fact]
    public async Task View_ForwardsCurrencyAndHeightToChart()
    {
        await using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddLogging();
        context.Services.AddMudServices();
        var cut = context.Render<AssetsTimeSeriesCard>(p => p
            .Add(x => x.CurrencyShortName, "USD").Add(x => x.Height, "380px"));
        var chart = cut.FindComponent<TimeSeriesValueCard>().Instance;
        Assert.Equal("USD", chart.CurrencyShortName);
        Assert.Equal("380px", chart.Height);
    }

    [Fact]
    public async Task HydratesBeforeRequestCompletes_AndFetchesOnEveryVisit()
    {
        Stored(10);
        var handler = new SeriesHandler();
        using var context = Context(handler);
        var first = Render(context);
        first.WaitForAssertion(() => Assert.Equal(10, View(first).Model!.Series.Single().Value));
        Assert.Equal(1, handler.Count);
        Assert.Equal("PLN", View(first).Currency);
        Assert.Equal("250px", View(first).Height);
        handler.Complete(0, Points(10));
        await Drain(first);
        first.WaitForAssertion(() => Assert.False(View(first).IsLoading));
        first.Dispose();
        var second = Render(context);
        second.WaitForAssertion(() => Assert.Equal(2, handler.Count));
        handler.Complete(1, Points(10));
        await second.InvokeAsync(() => Task.CompletedTask);
    }

    [Fact]
    public async Task EqualSeries_DoesNotWrite_DespiteDifferentCaptureTime()
    {
        Stored(10);
        var handler = new SeriesHandler();
        using var context = Context(handler);
        var cut = Render(context);
        handler.Complete(0, Points(10));
        await Drain(cut);
        _snapshots.Verify(x => x.SetAsync(It.IsAny<string>(), It.IsAny<AssetsTimeSeriesSnapshot>()), Times.Never);
    }

    [Fact]
    public async Task EmptySuccess_ReplacesPopulatedSnapshot()
    {
        Stored(10);
        var handler = new SeriesHandler();
        using var context = Context(handler);
        var cut = Render(context);
        handler.Complete(0, []);
        cut.WaitForAssertion(() => Assert.Empty(View(cut).Model!.Series));
        await Drain(cut);
        _snapshots.Verify(x => x.SetAsync(_key, It.Is<AssetsTimeSeriesSnapshot>(s => s.Series.Count == 0)), Times.Once);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task FailedRefresh_PreservesSnapshotOrShowsBlockingError(bool stored)
    {
        if (stored) Stored(10);
        var handler = new SeriesHandler();
        using var context = Context(handler);
        var cut = Render(context);
        handler.Fail(0);
        cut.WaitForAssertion(() =>
        {
            Assert.False(View(cut).IsLoading);
            Assert.Equal(!stored, View(cut).HasError);
            if (stored) Assert.Equal(10, View(cut).Model!.Series.Single().Value);
            else Assert.Null(View(cut).Model);
        });
        _snapshots.Verify(x => x.SetAsync(It.IsAny<string>(), It.IsAny<AssetsTimeSeriesSnapshot>()), Times.Never);
        await Drain(cut);
    }

    [Fact]
    public async Task RangeChange_SupersedesSlowRun_AndKeepsStableKeyWithActualDates()
    {
        var handler = new SeriesHandler();
        using var context = Context(handler);
        var cut = Render(context);
        var nextStart = _start.AddDays(5);
        cut.Render(parameters => parameters.Add(x => x.StartDateTime, nextStart).Add(x => x.EndDateTime, _end));
        cut.WaitForAssertion(() => Assert.Equal(2, handler.Count));
        handler.Complete(1, [new TimeSeriesModel(nextStart, 20)]);
        cut.WaitForAssertion(() => Assert.Equal(20, View(cut).Model!.Series.Single().Value));
        handler.Complete(0, Points(10));
        await Drain(cut);
        Assert.Equal(nextStart, View(cut).Model!.Series.Single().DateTime);
        _snapshots.Verify(x => x.SetAsync(_key, It.Is<AssetsTimeSeriesSnapshot>(s => s.StartDate == nextStart && s.EndDate == _end)), Times.Once);
        _snapshots.Verify(x => x.SetAsync(It.IsAny<string>(), It.Is<AssetsTimeSeriesSnapshot>(s => s.StartDate == _start)), Times.Never);
    }

    [Fact]
    public async Task DifferentStoredRange_IsNotPainted()
    {
        Stored(10, _start.AddDays(-20));
        var handler = new SeriesHandler();
        using var context = Context(handler);
        var cut = Render(context);
        Assert.Null(View(cut).Model);
        Assert.True(View(cut).IsLoading);
        handler.Complete(0, Points(20));
        cut.WaitForAssertion(() => Assert.Equal(_start, View(cut).Model!.Series.Single().DateTime));
        await Drain(cut);
    }

    [Fact]
    public async Task StorageReadFailure_StillFetchesAndRenders()
    {
        _snapshots.Setup(x => x.GetAsync<AssetsTimeSeriesSnapshot>(_key)).ThrowsAsync(new InvalidOperationException());
        var handler = new SeriesHandler();
        using var context = Context(handler);
        var cut = Render(context);
        handler.Complete(0, Points(20));
        cut.WaitForAssertion(() => Assert.Equal(20, View(cut).Model!.Series.Single().Value));
        _snapshots.Verify(x => x.RemoveAsync(_key), Times.Once);
        await Drain(cut);
    }

    [Fact]
    public async Task StorageWriteFailure_KeepsFreshChart()
    {
        _snapshots.Setup(x => x.SetAsync(It.IsAny<string>(), It.IsAny<AssetsTimeSeriesSnapshot>())).ThrowsAsync(new InvalidOperationException());
        var handler = new SeriesHandler();
        using var context = Context(handler);
        var cut = Render(context);
        handler.Complete(0, Points(20));
        cut.WaitForAssertion(() => Assert.Equal(20, View(cut).Model!.Series.Single().Value));
        await Drain(cut);
        Assert.False(View(cut).HasError);
    }

    [Fact]
    public async Task SharesSeriesRequestWithAssetsCache_AndSnapshotWriteIsIndependent()
    {
        var handler = new SeriesHandler();
        using var context = Context(handler);
        var cut = Render(context);
        var cache = context.Services.GetRequiredService<AssetsPageCardsCacheService>();
        var aggregate = cache.GetSnapshotAsync(new AssetsPageCardsRefreshContext
        {
            UserId = 1,
            CurrencyId = DefaultCurrency.PLN.Id,
            StartDateTime = _start,
            EndDateTime = _end,
        });
        Assert.Equal(1, handler.Count);
        handler.Complete(0, Points(20));
        await aggregate;
        cut.WaitForAssertion(() => Assert.Equal(20, View(cut).Model!.Series.Single().Value));
        await Drain(cut);
        _snapshots.Verify(x => x.SetAsync(_key, It.IsAny<AssetsTimeSeriesSnapshot>()), Times.Once);
        Assert.Single(_snapshots.Invocations, x => x.Method.Name == nameof(ISnapshotService.SetAsync));
    }

    private void Stored(decimal value, DateTime? start = null) =>
        _snapshots.Setup(x => x.GetAsync<AssetsTimeSeriesSnapshot>(_key)).ReturnsAsync(new AssetsTimeSeriesSnapshot
        {
            UserId = 1,
            CurrencyId = DefaultCurrency.PLN.Id,
            StartDate = start ?? _start,
            EndDate = _end.AddMinutes(-1),
            Series = Points(value),
            FetchedAtUtc = _start.AddYears(-1),
        });

    private BunitContext Context(SeriesHandler handler)
    {
        var context = new BunitContext();
        context.ComponentFactories.AddStub<AssetsTimeSeriesCard>();
        context.Services.AddLogging();
        var login = new Mock<ILoginService>();
        login.Setup(x => x.GetLoggedUser()).ReturnsAsync(new UserSession { UserId = 1, UserName = "tester", Password = "", UserRole = UserRole.User });
        var settings = new Mock<ISettingsService>();
        settings.Setup(x => x.GetCurrencyAsync()).ReturnsAsync(DefaultCurrency.PLN);
        context.Services.AddSingleton(login.Object);
        context.Services.AddSingleton(settings.Object);
        context.Services.AddSingleton<ISnapshotRefreshCoordinator>(new SnapshotRefreshCoordinator(_snapshots.Object, NullLogger<SnapshotRefreshCoordinator>.Instance));
        context.Services.AddSingleton(new AssetsPageCardsCacheService(Mock.Of<ILocalStorageService>(), new MemoryCache(new MemoryCacheOptions()),
            new AssetsHttpClient(new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") }), NullLogger<AssetsPageCardsCacheService>.Instance));
        return context;
    }

    private static IRenderedComponent<AssetsTimeSeriesCardContainer> Render(BunitContext context) =>
        context.Render<AssetsTimeSeriesCardContainer>(p => p.Add(x => x.StartDateTime, _start).Add(x => x.EndDateTime, _end));

    private static (TimeSeriesCardModel? Model, bool IsLoading, bool HasError, string Currency, string Height) View(IRenderedComponent<AssetsTimeSeriesCardContainer> cut)
    {
        var stub = cut.FindComponent<Bunit.TestDoubles.Stub<AssetsTimeSeriesCard>>();
        return (stub.Instance.Parameters.Get(x => x.Model), stub.Instance.Parameters.Get(x => x.IsLoading),
            stub.Instance.Parameters.Get(x => x.HasError), stub.Instance.Parameters.Get(x => x.CurrencyShortName),
            stub.Instance.Parameters.Get(x => x.Height));
    }

    private static Task Drain(IRenderedComponent<AssetsTimeSeriesCardContainer> cut) => cut.InvokeAsync(async () => await Task.Delay(50));
    private static List<TimeSeriesModel> Points(decimal value) => [new(_start, value)];

    private sealed class SeriesHandler : HttpMessageHandler
    {
        private readonly List<TaskCompletionSource<HttpResponseMessage>> _responses = [];
        public int Count => _responses.Count;
        public void Complete(int index, List<TimeSeriesModel> points) => _responses[index].SetResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(points) });
        public void Fail(int index) => _responses[index].SetResult(new HttpResponseMessage(HttpStatusCode.InternalServerError));
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (!request.RequestUri!.AbsolutePath.Contains("GetAssetsTimeSeries", StringComparison.Ordinal))
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(Array.Empty<object>()) });
            var response = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
            _responses.Add(response);
            return response.Task;
        }
    }
}