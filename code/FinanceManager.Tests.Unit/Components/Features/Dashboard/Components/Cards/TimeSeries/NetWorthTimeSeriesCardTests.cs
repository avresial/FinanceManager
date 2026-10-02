using Bunit;
using FinanceManager.Components.Features.Dashboard.Components.Cards.TimeSeries;
using FinanceManager.Components.Features.Dashboard.Models;
using FinanceManager.Components.Features.MoneyFlow.HttpClients;
using FinanceManager.Components.Shared.Components.Charts;
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

namespace FinanceManager.Tests.Unit.Components.Features.Dashboard.Components.Cards.TimeSeries;

[Trait("Category", "Unit")]
public class NetWorthTimeSeriesCardTests
{
    private static readonly DateTime _start = new(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime _end = _start.AddDays(30).AddHours(12);
    private static readonly string _key = $"networth-timeseries:1:{DefaultCurrency.PLN.Id}";
    private readonly Mock<ISnapshotService> _snapshots = new();

    [Fact]
    public async Task HydratesBeforeRequestCompletes_AndFetchesOnEveryVisit()
    {
        Stored(10);
        var handler = new NetWorthHandler();
        using var context = Context(handler);
        var first = Render(context);
        first.WaitForAssertion(() => Assert.Equal(10, View(first).Data.Single().Value));
        Assert.Equal(1, handler.Count);
        Assert.Equal("PLN", View(first).Currency);
        handler.Complete(0, Points(10));
        await Drain(first);
        first.WaitForAssertion(() => Assert.False(View(first).IsLoading));
        first.Dispose();
        var second = Render(context);
        second.WaitForAssertion(() => Assert.Equal(2, handler.Count));
        handler.Complete(1, Points(10));
        await Drain(second);
    }

    [Fact]
    public async Task EqualSeries_DoesNotWrite_DespiteDifferentCaptureTime()
    {
        Stored(10);
        var handler = new NetWorthHandler();
        using var context = Context(handler);
        var cut = Render(context);
        cut.WaitForAssertion(() => Assert.Equal(1, handler.Count));
        handler.Complete(0, Points(10));
        await Drain(cut);
        _snapshots.Verify(x => x.SetAsync(It.IsAny<string>(), It.IsAny<NetWorthTimeSeriesSnapshot>()), Times.Never);
    }

    [Fact]
    public async Task ChangedSeries_RepaintsAndWritesWithKeyAndRange()
    {
        Stored(10);
        var handler = new NetWorthHandler();
        using var context = Context(handler);
        var cut = Render(context);
        cut.WaitForAssertion(() => Assert.Equal(1, handler.Count));
        handler.Complete(0, Points(30));
        cut.WaitForAssertion(() => Assert.Equal(30, View(cut).Data.Single().Value));
        await Drain(cut);
        _snapshots.Verify(x => x.SetAsync(_key, It.Is<NetWorthTimeSeriesSnapshot>(s =>
            s.UserId == 1 && s.CurrencyId == DefaultCurrency.PLN.Id && s.StartDate == _start && s.EndDate == _end
            && s.Series.Count == 1 && s.Series[0].Value == 30)), Times.Once);
    }

    [Fact]
    public async Task EmptySuccess_ClearsAndWrites()
    {
        Stored(10);
        var handler = new NetWorthHandler();
        using var context = Context(handler);
        var cut = Render(context);
        cut.WaitForAssertion(() => Assert.Equal(1, handler.Count));
        handler.Complete(0, []);
        cut.WaitForAssertion(() => Assert.Empty(View(cut).Data));
        await Drain(cut);
        Assert.False(View(cut).HasError);
        _snapshots.Verify(x => x.SetAsync(_key, It.Is<NetWorthTimeSeriesSnapshot>(s => s.Series.Count == 0)), Times.Once);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task FailedRefresh_PreservesSnapshotOrShowsBlockingError(bool stored)
    {
        if (stored) Stored(10);
        var handler = new NetWorthHandler();
        using var context = Context(handler);
        var cut = Render(context);
        cut.WaitForAssertion(() => Assert.Equal(1, handler.Count));
        handler.Fail(0);
        cut.WaitForAssertion(() =>
        {
            Assert.False(View(cut).IsLoading);
            Assert.Equal(!stored, View(cut).HasError);
            if (stored) Assert.Equal(10, View(cut).Data.Single().Value);
            else Assert.Empty(View(cut).Data);
        });
        _snapshots.Verify(x => x.SetAsync(It.IsAny<string>(), It.IsAny<NetWorthTimeSeriesSnapshot>()), Times.Never);
        await Drain(cut);
    }

    [Theory]
    [InlineData(-20, 1, 0)]
    [InlineData(0, 2, 0)]
    [InlineData(0, 1, 5)]
    public async Task SnapshotForDifferentRangeUserOrCurrency_IsNotPainted(int startOffsetDays, int userId, int currencyId)
    {
        _snapshots.Setup(x => x.GetAsync<NetWorthTimeSeriesSnapshot>(_key)).ReturnsAsync(new NetWorthTimeSeriesSnapshot
        {
            UserId = userId,
            CurrencyId = currencyId == 0 ? DefaultCurrency.PLN.Id : currencyId,
            StartDate = _start.AddDays(startOffsetDays),
            EndDate = _end,
            Series = Points(10),
        });
        var handler = new NetWorthHandler();
        using var context = Context(handler);
        var cut = Render(context);
        cut.WaitForAssertion(() => Assert.Equal(1, handler.Count));
        Assert.Empty(View(cut).Data);
        Assert.True(View(cut).IsLoading);
        handler.Complete(0, Points(20));
        cut.WaitForAssertion(() => Assert.Equal(20, View(cut).Data.Single().Value));
        await Drain(cut);
    }

    [Fact]
    public async Task RangeChange_SupersedesSlowRun()
    {
        var handler = new NetWorthHandler();
        using var context = Context(handler);
        var cut = Render(context);
        cut.WaitForAssertion(() => Assert.Equal(1, handler.Count));
        var nextStart = _start.AddDays(5);
        cut.Render(p => p.Add(x => x.StartDateTime, nextStart).Add(x => x.EndDateTime, _end));
        cut.WaitForAssertion(() => Assert.Equal(2, handler.Count));
        handler.Complete(1, [new TimeSeriesModel(nextStart, 20)]);
        cut.WaitForAssertion(() => Assert.Equal(20, View(cut).Data.Single().Value));
        handler.Complete(0, Points(10));
        await Drain(cut);
        Assert.Equal(nextStart, View(cut).Data.Single().DateTime);
        _snapshots.Verify(x => x.SetAsync(_key, It.Is<NetWorthTimeSeriesSnapshot>(s => s.StartDate == nextStart)), Times.Once);
        _snapshots.Verify(x => x.SetAsync(It.IsAny<string>(), It.Is<NetWorthTimeSeriesSnapshot>(s => s.StartDate == _start)), Times.Never);
    }

    [Fact]
    public async Task SuppliedModel_RendersDirectly_WithoutRequestOrSnapshot()
    {
        var handler = new NetWorthHandler();
        using var context = Context(handler);
        var cut = context.Render<NetWorthTimeSeriesCard>(p => p
            .Add(x => x.StartDateTime, _start).Add(x => x.EndDateTime, _end)
            .Add(x => x.Model, new TimeSeriesCardModel(Points(7))));
        cut.WaitForAssertion(() => Assert.Equal(7, View(cut).Data.Single().Value));
        await Drain(cut);
        Assert.Equal(0, handler.Count);
        Assert.Empty(_snapshots.Invocations);
    }

    [Fact]
    public async Task SuppliedModel_SupersedesInFlightFallbackLoad()
    {
        var handler = new NetWorthHandler();
        using var context = Context(handler);
        var cut = Render(context);
        cut.WaitForAssertion(() => Assert.Equal(1, handler.Count));
        cut.Render(p => p.Add(x => x.StartDateTime, _start).Add(x => x.EndDateTime, _end)
            .Add(x => x.Model, new TimeSeriesCardModel(Points(7))));
        cut.WaitForAssertion(() => Assert.Equal(7, View(cut).Data.Single().Value));
        handler.Complete(0, Points(99));
        await Drain(cut);
        Assert.Equal(7, View(cut).Data.Single().Value);
        Assert.False(View(cut).IsLoading);
        _snapshots.Verify(x => x.SetAsync(It.IsAny<string>(), It.IsAny<NetWorthTimeSeriesSnapshot>()), Times.Never);
    }

    [Fact]
    public async Task StorageReadFailure_StillFetchesAndRenders()
    {
        _snapshots.Setup(x => x.GetAsync<NetWorthTimeSeriesSnapshot>(_key)).ThrowsAsync(new InvalidOperationException());
        var handler = new NetWorthHandler();
        using var context = Context(handler);
        var cut = Render(context);
        cut.WaitForAssertion(() => Assert.Equal(1, handler.Count));
        handler.Complete(0, Points(20));
        cut.WaitForAssertion(() => Assert.Equal(20, View(cut).Data.Single().Value));
        _snapshots.Verify(x => x.RemoveAsync(_key), Times.Once);
        await Drain(cut);
    }

    [Fact]
    public async Task StorageWriteFailure_KeepsFreshChart()
    {
        _snapshots.Setup(x => x.SetAsync(It.IsAny<string>(), It.IsAny<NetWorthTimeSeriesSnapshot>())).ThrowsAsync(new InvalidOperationException());
        var handler = new NetWorthHandler();
        using var context = Context(handler);
        var cut = Render(context);
        cut.WaitForAssertion(() => Assert.Equal(1, handler.Count));
        handler.Complete(0, Points(20));
        cut.WaitForAssertion(() => Assert.Equal(20, View(cut).Data.Single().Value));
        await Drain(cut);
        Assert.False(View(cut).HasError);
    }

    private void Stored(decimal value) =>
        _snapshots.Setup(x => x.GetAsync<NetWorthTimeSeriesSnapshot>(_key)).ReturnsAsync(new NetWorthTimeSeriesSnapshot
        {
            UserId = 1,
            CurrencyId = DefaultCurrency.PLN.Id,
            StartDate = _start,
            EndDate = _end.AddMinutes(-1),
            Series = Points(value),
            FetchedAtUtc = _start.AddYears(-1),
        });

    private BunitContext Context(NetWorthHandler handler)
    {
        var context = new BunitContext();
        context.ComponentFactories.AddStub<TimeSeriesValueCard>();
        context.Services.AddLogging();
        var login = new Mock<ILoginService>();
        login.Setup(x => x.GetLoggedUser()).ReturnsAsync(new UserSession { UserId = 1, UserName = "tester", Password = "", UserRole = UserRole.User });
        var settings = new Mock<ISettingsService>();
        settings.Setup(x => x.GetCurrencyAsync()).ReturnsAsync(DefaultCurrency.PLN);
        context.Services.AddSingleton(login.Object);
        context.Services.AddSingleton(settings.Object);
        context.Services.AddSingleton<ISnapshotRefreshCoordinator>(new SnapshotRefreshCoordinator(_snapshots.Object, NullLogger<SnapshotRefreshCoordinator>.Instance));
        context.Services.AddSingleton(new MoneyFlowHttpClient(new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") }));
        return context;
    }

    private static IRenderedComponent<NetWorthTimeSeriesCard> Render(BunitContext context) =>
        context.Render<NetWorthTimeSeriesCard>(p => p.Add(x => x.StartDateTime, _start).Add(x => x.EndDateTime, _end));

    private static (IReadOnlyList<TimeSeriesModel> Data, bool IsLoading, bool HasError, string Currency) View(IRenderedComponent<NetWorthTimeSeriesCard> cut)
    {
        var stub = cut.FindComponent<Bunit.TestDoubles.Stub<TimeSeriesValueCard>>();
        return (stub.Instance.Parameters.Get(x => x.Data), stub.Instance.Parameters.Get(x => x.IsLoading),
            stub.Instance.Parameters.Get(x => x.HasError), stub.Instance.Parameters.Get(x => x.CurrencyShortName));
    }

    private static Task Drain(IRenderedComponent<NetWorthTimeSeriesCard> cut) => cut.InvokeAsync(async () => await Task.Delay(50));
    private static List<TimeSeriesModel> Points(decimal value) => [new(_start, value)];

    private sealed class NetWorthHandler : HttpMessageHandler
    {
        private readonly List<TaskCompletionSource<HttpResponseMessage>> _responses = [];
        public int Count => _responses.Count;

        public void Complete(int index, List<TimeSeriesModel> points) =>
            _responses[index].SetResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(points.ToDictionary(x => x.DateTime, x => x.Value)),
            });

        public void Fail(int index) => _responses[index].SetResult(new HttpResponseMessage(HttpStatusCode.InternalServerError));

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
            _responses.Add(response);
            return response.Task;
        }
    }
}