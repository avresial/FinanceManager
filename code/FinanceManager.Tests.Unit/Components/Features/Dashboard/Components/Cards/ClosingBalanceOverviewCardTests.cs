using Bunit;
using FinanceManager.Components.Features.Dashboard.Components.Cards;
using FinanceManager.Components.Features.Dashboard.Models;
using FinanceManager.Components.Features.Dashboard.Services;
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

namespace FinanceManager.Tests.Unit.Components.Features.Dashboard.Components.Cards;

[Trait("Category", "Unit")]
public class ClosingBalanceOverviewCardTests
{
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(5);
    private static readonly DateTime _start = new(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime _end = _start.AddDays(30).AddHours(12);
    private static readonly string _key = $"closing-balance:1:{DefaultCurrency.PLN.Id}";
    private readonly Mock<ISnapshotService> _snapshots = new();

    [Fact]
    public async Task HydratesBeforeRequestCompletes_AndFetchesOnEveryVisit()
    {
        Stored(10);
        var handler = new ClosingBalanceHandler();
        using var context = Context(handler);
        var first = Render(context);
        first.WaitForAssertion(() => Assert.Equal(10, View(first).Data.Single().Value), _timeout);
        Assert.Equal(1, handler.Count);
        Assert.Equal("PLN", View(first).Currency);
        handler.Complete(0, Points(10));
        await Drain(first);
        first.WaitForAssertion(() => Assert.False(View(first).IsLoading), _timeout);
        first.Dispose();
        var second = Render(context);
        second.WaitForAssertion(() => Assert.Equal(2, handler.Count), _timeout);
        handler.Complete(1, Points(10));
        await Drain(second);
    }

    [Fact]
    public async Task EqualSeries_DoesNotWrite_DespiteDifferentCaptureTime()
    {
        Stored(10);
        var handler = new ClosingBalanceHandler();
        using var context = Context(handler);
        var cut = Render(context);
        cut.WaitForAssertion(() => Assert.Equal(1, handler.Count), _timeout);
        handler.Complete(0, Points(10));
        await Drain(cut);
        _snapshots.Verify(x => x.SetAsync(It.IsAny<string>(), It.IsAny<ClosingBalanceSnapshot>()), Times.Never);
    }

    [Fact]
    public async Task ChangedSeries_RepaintsAndWritesWithKeyAndRange()
    {
        Stored(10);
        var handler = new ClosingBalanceHandler();
        using var context = Context(handler);
        var cut = Render(context);
        cut.WaitForAssertion(() => Assert.Equal(1, handler.Count), _timeout);
        handler.Complete(0, [new(_start, 30.004m), new(_start.AddDays(1), 5)]);
        cut.WaitForAssertion(() => Assert.Equal(30m, View(cut).Data.First().Value), _timeout);
        Assert.Equal(30m, View(cut).Data.First().Value);
        await Drain(cut);
        _snapshots.Verify(x => x.SetAsync(_key, It.Is<ClosingBalanceSnapshot>(s =>
            s.UserId == 1 && s.CurrencyId == DefaultCurrency.PLN.Id && s.StartDate == _start && s.EndDate == _end
            && s.Series.Count == 2 && s.Series[0].Value == 30)), Times.Once);
    }

    [Fact]
    public async Task EmptySuccess_ClearsAndWrites()
    {
        Stored(10);
        var handler = new ClosingBalanceHandler();
        using var context = Context(handler);
        var cut = Render(context);
        cut.WaitForAssertion(() => Assert.Equal(1, handler.Count), _timeout);
        handler.Complete(0, []);
        cut.WaitForAssertion(() => Assert.Empty(View(cut).Data), _timeout);
        await Drain(cut);
        Assert.False(View(cut).HasError);
        _snapshots.Verify(x => x.SetAsync(_key, It.Is<ClosingBalanceSnapshot>(s => s.Series.Count == 0)), Times.Once);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task NullResponse_PreservesSnapshotOrShowsBlockingError(bool stored)
    {
        if (stored) Stored(10);
        var handler = new ClosingBalanceHandler();
        using var context = Context(handler);
        var cut = Render(context);
        cut.WaitForAssertion(() => Assert.Equal(1, handler.Count), _timeout);
        handler.Complete(0, null);
        cut.WaitForAssertion(() =>
        {
            Assert.False(View(cut).IsLoading);
            Assert.Equal(!stored, View(cut).HasError);
            if (stored) Assert.Equal(10, View(cut).Data.Single().Value);
            else Assert.Empty(View(cut).Data);
        }, _timeout);
        await Drain(cut);
        _snapshots.Verify(x => x.SetAsync(It.IsAny<string>(), It.IsAny<ClosingBalanceSnapshot>()), Times.Never);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task FailedRefresh_PreservesSnapshotOrShowsBlockingError(bool stored)
    {
        if (stored) Stored(10);
        var handler = new ClosingBalanceHandler();
        using var context = Context(handler);
        var cut = Render(context);
        cut.WaitForAssertion(() => Assert.Equal(1, handler.Count), _timeout);
        handler.Fail(0);
        cut.WaitForAssertion(() =>
        {
            Assert.False(View(cut).IsLoading);
            Assert.Equal(!stored, View(cut).HasError);
            if (stored) Assert.Equal(10, View(cut).Data.Single().Value);
            else Assert.Empty(View(cut).Data);
        }, _timeout);
        _snapshots.Verify(x => x.SetAsync(It.IsAny<string>(), It.IsAny<ClosingBalanceSnapshot>()), Times.Never);
        await Drain(cut);
    }

    [Theory]
    [InlineData(-20, 1, 0)]
    [InlineData(0, 2, 0)]
    [InlineData(0, 1, 5)]
    public async Task SnapshotForDifferentRangeUserOrCurrency_IsNotPainted(int startOffsetDays, int userId, int currencyId)
    {
        _snapshots.Setup(x => x.GetAsync<ClosingBalanceSnapshot>(_key)).ReturnsAsync(new ClosingBalanceSnapshot
        {
            UserId = userId,
            CurrencyId = currencyId == 0 ? DefaultCurrency.PLN.Id : currencyId,
            StartDate = _start.AddDays(startOffsetDays),
            EndDate = _end,
            Series = Points(10),
        });
        var handler = new ClosingBalanceHandler();
        using var context = Context(handler);
        var cut = Render(context);
        cut.WaitForAssertion(() => Assert.Equal(1, handler.Count), _timeout);
        Assert.Empty(View(cut).Data);
        Assert.True(View(cut).IsLoading);
        handler.Complete(0, Points(20));
        cut.WaitForAssertion(() => Assert.Equal(20, View(cut).Data.Single().Value), _timeout);
        await Drain(cut);
    }

    [Fact]
    public async Task RangeChange_SupersedesSlowRun()
    {
        var handler = new ClosingBalanceHandler();
        using var context = Context(handler);
        var cut = Render(context);
        cut.WaitForAssertion(() => Assert.Equal(1, handler.Count), _timeout);
        var nextStart = _start.AddDays(5);
        cut.Render(p => p.Add(x => x.StartDateTime, nextStart).Add(x => x.EndDateTime, _end));
        cut.WaitForAssertion(() => Assert.Equal(2, handler.Count), _timeout);
        handler.Complete(1, [new TimeSeriesModel(nextStart, 20)]);
        cut.WaitForAssertion(() => Assert.Equal(20, View(cut).Data.Single().Value), _timeout);
        handler.Complete(0, Points(10));
        await Drain(cut);
        Assert.Equal(nextStart, View(cut).Data.Single().DateTime);
        _snapshots.Verify(x => x.SetAsync(_key, It.Is<ClosingBalanceSnapshot>(s => s.StartDate == nextStart)), Times.Once);
        _snapshots.Verify(x => x.SetAsync(It.IsAny<string>(), It.Is<ClosingBalanceSnapshot>(s => s.StartDate == _start)), Times.Never);
    }

    [Fact]
    public async Task SuppliedModel_RendersDirectly_WithoutRequestOrSnapshot()
    {
        var handler = new ClosingBalanceHandler();
        using var context = Context(handler);
        var cut = context.Render<ClosingBalanceOverviewCard>(p => p
            .Add(x => x.StartDateTime, _start).Add(x => x.EndDateTime, _end)
            .Add(x => x.Model, new TimeSeriesCardModel(Points(7))));
        cut.WaitForAssertion(() => Assert.Equal(7, View(cut).Data.Single().Value), _timeout);
        await Drain(cut);
        Assert.Equal(0, handler.Count);
        Assert.Empty(_snapshots.Invocations);
    }

    [Fact]
    public async Task SuppliedModel_SupersedesInFlightFallbackLoad()
    {
        var handler = new ClosingBalanceHandler();
        using var context = Context(handler);
        var cut = Render(context);
        cut.WaitForAssertion(() => Assert.Equal(1, handler.Count), _timeout);
        cut.Render(p => p.Add(x => x.StartDateTime, _start).Add(x => x.EndDateTime, _end)
            .Add(x => x.Model, new TimeSeriesCardModel(Points(7))));
        cut.WaitForAssertion(() => Assert.Equal(7, View(cut).Data.Single().Value), _timeout);
        handler.Complete(0, Points(99));
        await Drain(cut);
        Assert.Equal(7, View(cut).Data.Single().Value);
        Assert.False(View(cut).IsLoading);
        _snapshots.Verify(x => x.SetAsync(It.IsAny<string>(), It.IsAny<ClosingBalanceSnapshot>()), Times.Never);
    }

    [Fact]
    public async Task StorageReadFailure_StillFetchesAndRenders()
    {
        _snapshots.Setup(x => x.GetAsync<ClosingBalanceSnapshot>(_key)).ThrowsAsync(new InvalidOperationException());
        var handler = new ClosingBalanceHandler();
        using var context = Context(handler);
        var cut = Render(context);
        cut.WaitForAssertion(() => Assert.Equal(1, handler.Count), _timeout);
        handler.Complete(0, Points(20));
        cut.WaitForAssertion(() => Assert.Equal(20, View(cut).Data.Single().Value), _timeout);
        _snapshots.Verify(x => x.RemoveAsync(_key), Times.Once);
        await Drain(cut);
    }

    [Fact]
    public async Task StorageWriteFailure_KeepsFreshSeries()
    {
        _snapshots.Setup(x => x.SetAsync(It.IsAny<string>(), It.IsAny<ClosingBalanceSnapshot>())).ThrowsAsync(new InvalidOperationException());
        var handler = new ClosingBalanceHandler();
        using var context = Context(handler);
        var cut = Render(context);
        cut.WaitForAssertion(() => Assert.Equal(1, handler.Count), _timeout);
        handler.Complete(0, Points(20));
        cut.WaitForAssertion(() => Assert.Equal(20, View(cut).Data.Single().Value), _timeout);
        await Drain(cut);
        Assert.False(View(cut).HasError);
    }

    [Fact]
    public async Task SnapshotKey_IsScopedByUserAndCurrency()
    {
        var handler = new ClosingBalanceHandler();
        using var context = Context(handler);
        var cut = Render(context);
        cut.WaitForAssertion(() => Assert.Equal(1, handler.Count), _timeout);
        _snapshots.Verify(x => x.GetAsync<ClosingBalanceSnapshot>($"closing-balance:1:{DefaultCurrency.PLN.Id}"), Times.Once);
        handler.Complete(0, Points(1));
        await Drain(cut);
    }

    private void Stored(decimal value) =>
        _snapshots.Setup(x => x.GetAsync<ClosingBalanceSnapshot>(_key)).ReturnsAsync(new ClosingBalanceSnapshot
        {
            UserId = 1,
            CurrencyId = DefaultCurrency.PLN.Id,
            StartDate = _start,
            EndDate = _end.AddMinutes(-1),
            Series = Points(value),
            FetchedAtUtc = _start.AddYears(-1),
        });

    private BunitContext Context(ClosingBalanceHandler handler)
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
        context.Services.AddSingleton(new DashboardOverviewCardsCacheService(
            new MoneyFlowHttpClient(new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") })));
        return context;
    }

    private static IRenderedComponent<ClosingBalanceOverviewCard> Render(BunitContext context) =>
        context.Render<ClosingBalanceOverviewCard>(p => p.Add(x => x.StartDateTime, _start).Add(x => x.EndDateTime, _end));

    private static (IReadOnlyList<TimeSeriesModel> Data, bool IsLoading, bool HasError, string Currency) View(IRenderedComponent<ClosingBalanceOverviewCard> cut)
    {
        var stub = cut.FindComponent<Bunit.TestDoubles.Stub<TimeSeriesValueCard>>();
        return (stub.Instance.Parameters.Get(x => x.Data),
            stub.Instance.Parameters.Get(x => x.IsLoading), stub.Instance.Parameters.Get(x => x.HasError),
            stub.Instance.Parameters.Get(x => x.CurrencyShortName));
    }

    private static Task Drain(IRenderedComponent<ClosingBalanceOverviewCard> cut) => cut.InvokeAsync(async () => await Task.Delay(50));
    private static List<TimeSeriesModel> Points(decimal value) => [new(_start, value)];

    // The aggregate fetch issues a net-cash-flow and a closing-balance request; only the
    // closing-balance one is controlled by the tests, net cash flow answers immediately.
    private sealed class ClosingBalanceHandler : HttpMessageHandler
    {
        private readonly List<TaskCompletionSource<HttpResponseMessage>> _responses = [];
        public int Count => _responses.Count;

        public void Complete(int index, List<TimeSeriesModel>? points) =>
            _responses[index].SetResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(points) });

        public void Fail(int index) => _responses[index].SetResult(new HttpResponseMessage(HttpStatusCode.InternalServerError));

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (!request.RequestUri!.AbsolutePath.Contains("GetClosingBalance"))
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new List<TimeSeriesModel>()) });
            var response = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
            lock (_responses) _responses.Add(response);
            return response.Task;
        }
    }
}