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
using MudBlazor;
using System.Net;
using System.Net.Http.Json;

namespace FinanceManager.Tests.Unit.Components.Features.Dashboard.Components.Cards;

[Trait("Category", "Unit")]
public class ExpenseDistributionOverviewCardTests
{
    private static readonly DateTime _start = new(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime _end = _start.AddDays(30).AddHours(12);
    private static readonly string _key = $"expense-distribution:1:{DefaultCurrency.PLN.Id}";
    private readonly Mock<ISnapshotService> _snapshots = new();
    private readonly Mock<ISnackbar> _snackbar = new();
    private static readonly TimeSpan _wait = TimeSpan.FromSeconds(5);

    [Fact]
    public async Task HydratesBeforeRequestCompletes_AndFetchesOnEveryVisit()
    {
        Stored(10);
        var handler = new DistributionHandler();
        using var context = Context(handler);
        var first = Render(context);
        first.WaitForAssertion(() => Assert.Equal(10, View(first).Data.Single().Value), _wait);
        Assert.Equal(1, handler.Count);
        Assert.Equal("PLN", View(first).Currency);
        handler.Complete(0, Items(10));
        await Drain(first);
        first.WaitForAssertion(() => Assert.False(View(first).IsLoading), _wait);
        first.Dispose();
        var second = Render(context);
        second.WaitForAssertion(() => Assert.Equal(2, handler.Count), _wait);
        handler.Complete(1, Items(10));
        await Drain(second);
    }

    [Fact]
    public async Task EqualItems_DoesNotWrite_DespiteDifferentCaptureTime()
    {
        Stored(10);
        var handler = new DistributionHandler();
        using var context = Context(handler);
        var cut = Render(context);
        cut.WaitForAssertion(() => Assert.Equal(1, handler.Count), _wait);
        handler.Complete(0, Items(10));
        await Drain(cut);
        _snapshots.Verify(x => x.SetAsync(It.IsAny<string>(), It.IsAny<ExpenseDistributionSnapshot>()), Times.Never);
    }

    [Fact]
    public async Task ChangedItems_RepaintsAndWritesWithKeyAndRange()
    {
        Stored(10);
        var handler = new DistributionHandler();
        using var context = Context(handler);
        var cut = Render(context);
        cut.WaitForAssertion(() => Assert.Equal(1, handler.Count), _wait);
        handler.Complete(0, Items(30));
        cut.WaitForAssertion(() => Assert.Equal(30, View(cut).Data.Single().Value), _wait);
        await Drain(cut);
        _snapshots.Verify(x => x.SetAsync(_key, It.Is<ExpenseDistributionSnapshot>(s =>
            s.UserId == 1 && s.CurrencyId == DefaultCurrency.PLN.Id && s.StartDate == _start && s.EndDate == _end
            && s.Items.Count == 1 && s.Items[0].Value == 30)), Times.Once);
    }

    [Fact]
    public async Task EmptySuccess_ClearsAndWrites()
    {
        Stored(10);
        var handler = new DistributionHandler();
        using var context = Context(handler);
        var cut = Render(context);
        cut.WaitForAssertion(() => Assert.Equal(1, handler.Count), _wait);
        handler.Complete(0, []);
        cut.WaitForAssertion(() => Assert.Empty(View(cut).Data), _wait);
        await Drain(cut);
        VerifyNoSnackbar();
        _snapshots.Verify(x => x.SetAsync(_key, It.Is<ExpenseDistributionSnapshot>(s => s.Items.Count == 0)), Times.Once);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task FailedRefresh_PreservesSnapshotSilently_OrShowsSnackbarWhenNothingPainted(bool stored)
    {
        if (stored) Stored(10);
        var handler = new DistributionHandler();
        using var context = Context(handler);
        var cut = Render(context);
        cut.WaitForAssertion(() => Assert.Equal(1, handler.Count), _wait);
        handler.Fail(0);
        cut.WaitForAssertion(() =>
        {
            Assert.False(View(cut).IsLoading);
            if (stored) Assert.Equal(10, View(cut).Data.Single().Value);
            else Assert.Empty(View(cut).Data);
        }, _wait);
        _snapshots.Verify(x => x.SetAsync(It.IsAny<string>(), It.IsAny<ExpenseDistributionSnapshot>()), Times.Never);
        await Drain(cut);
        if (stored) VerifyNoSnackbar();
        else _snackbar.Verify(x => x.Add("Unable to load expense distribution.", Severity.Error, It.IsAny<Action<SnackbarOptions>>(), It.IsAny<string>()), Times.Once);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task NullResponse_PreservesSnapshotSilently_OrShowsSnackbarWhenNothingPainted(bool stored)
    {
        if (stored) Stored(10);
        var handler = new DistributionHandler();
        using var context = Context(handler);
        var cut = Render(context);
        cut.WaitForAssertion(() => Assert.Equal(1, handler.Count), _wait);
        handler.Complete(0, null);
        cut.WaitForAssertion(() =>
        {
            Assert.False(View(cut).IsLoading);
            if (stored) Assert.Equal(10, View(cut).Data.Single().Value);
            else Assert.Empty(View(cut).Data);
        }, _wait);
        await Drain(cut);
        _snapshots.Verify(x => x.SetAsync(It.IsAny<string>(), It.IsAny<ExpenseDistributionSnapshot>()), Times.Never);
        if (stored) VerifyNoSnackbar();
        else _snackbar.Verify(x => x.Add("Unable to load expense distribution.", Severity.Error, It.IsAny<Action<SnackbarOptions>>(), It.IsAny<string>()), Times.Once);
    }

    [Theory]
    [InlineData(-20, 1, 0)]
    [InlineData(0, 2, 0)]
    [InlineData(0, 1, 5)]
    public async Task SnapshotForDifferentRangeUserOrCurrency_IsNotPainted(int startOffsetDays, int userId, int currencyId)
    {
        _snapshots.Setup(x => x.GetAsync<ExpenseDistributionSnapshot>(_key)).ReturnsAsync(new ExpenseDistributionSnapshot
        {
            UserId = userId,
            CurrencyId = currencyId == 0 ? DefaultCurrency.PLN.Id : currencyId,
            StartDate = _start.AddDays(startOffsetDays),
            EndDate = _end,
            Items = Items(10),
        });
        var handler = new DistributionHandler();
        using var context = Context(handler);
        var cut = Render(context);
        cut.WaitForAssertion(() => Assert.Equal(1, handler.Count), _wait);
        Assert.Empty(View(cut).Data);
        Assert.True(View(cut).IsLoading);
        handler.Complete(0, Items(20));
        cut.WaitForAssertion(() => Assert.Equal(20, View(cut).Data.Single().Value), _wait);
        await Drain(cut);
    }

    [Fact]
    public async Task RangeChange_SupersedesSlowRun()
    {
        var handler = new DistributionHandler();
        using var context = Context(handler);
        var cut = Render(context);
        cut.WaitForAssertion(() => Assert.Equal(1, handler.Count), _wait);
        var nextStart = _start.AddDays(5);
        cut.Render(p => p.Add(x => x.StartDateTime, nextStart).Add(x => x.EndDateTime, _end));
        cut.WaitForAssertion(() => Assert.Equal(2, handler.Count), _wait);
        handler.Complete(1, Items(20));
        cut.WaitForAssertion(() => Assert.Equal(20, View(cut).Data.Single().Value), _wait);
        handler.Complete(0, Items(10));
        await Drain(cut);
        Assert.Equal(20, View(cut).Data.Single().Value);
        _snapshots.Verify(x => x.SetAsync(_key, It.Is<ExpenseDistributionSnapshot>(s => s.StartDate == nextStart)), Times.Once);
        _snapshots.Verify(x => x.SetAsync(It.IsAny<string>(), It.Is<ExpenseDistributionSnapshot>(s => s.StartDate == _start)), Times.Never);
    }

    [Fact]
    public async Task SuppliedModel_RendersDirectly_WithoutRequestOrSnapshot()
    {
        var handler = new DistributionHandler();
        using var context = Context(handler);
        var cut = context.Render<ExpenseDistributionOverviewCard>(p => p
            .Add(x => x.StartDateTime, _start).Add(x => x.EndDateTime, _end)
            .Add(x => x.Model, new NameValueListCardModel(Items(7))));
        cut.WaitForAssertion(() => Assert.Equal(7, View(cut).Data.Single().Value), _wait);
        await Drain(cut);
        Assert.Equal(0, handler.Count);
        Assert.Empty(_snapshots.Invocations);
    }

    [Fact]
    public async Task SuppliedModel_SupersedesInFlightFallbackLoad()
    {
        var handler = new DistributionHandler();
        using var context = Context(handler);
        var cut = Render(context);
        cut.WaitForAssertion(() => Assert.Equal(1, handler.Count), _wait);
        cut.Render(p => p.Add(x => x.StartDateTime, _start).Add(x => x.EndDateTime, _end)
            .Add(x => x.Model, new NameValueListCardModel(Items(7))));
        cut.WaitForAssertion(() => Assert.Equal(7, View(cut).Data.Single().Value), _wait);
        handler.Complete(0, Items(99));
        await Drain(cut);
        Assert.Equal(7, View(cut).Data.Single().Value);
        Assert.False(View(cut).IsLoading);
        _snapshots.Verify(x => x.SetAsync(It.IsAny<string>(), It.IsAny<ExpenseDistributionSnapshot>()), Times.Never);
    }

    [Fact]
    public async Task ZeroValueItems_AreKeptInRenderAndSnapshot()
    {
        var handler = new DistributionHandler();
        using var context = Context(handler);
        var cut = Render(context);
        cut.WaitForAssertion(() => Assert.Equal(1, handler.Count), _wait);
        handler.Complete(0, [new("Food", 20), new("Zero", 0), new("Rent", -5)]);
        cut.WaitForAssertion(() => Assert.Equal(["Food", "Zero", "Rent"], View(cut).Data.Select(x => x.Name)), _wait);
        await Drain(cut);
        _snapshots.Verify(x => x.SetAsync(_key, It.Is<ExpenseDistributionSnapshot>(s =>
            s.Items.Select(i => i.Name).SequenceEqual(new[] { "Food", "Zero", "Rent" }))), Times.Once);
    }

    [Fact]
    public async Task StorageReadFailure_StillFetchesAndRenders()
    {
        _snapshots.Setup(x => x.GetAsync<ExpenseDistributionSnapshot>(_key)).ThrowsAsync(new InvalidOperationException());
        var handler = new DistributionHandler();
        using var context = Context(handler);
        var cut = Render(context);
        cut.WaitForAssertion(() => Assert.Equal(1, handler.Count), _wait);
        handler.Complete(0, Items(20));
        cut.WaitForAssertion(() => Assert.Equal(20, View(cut).Data.Single().Value), _wait);
        _snapshots.Verify(x => x.RemoveAsync(_key), Times.Once);
        await Drain(cut);
    }

    [Fact]
    public async Task StorageWriteFailure_KeepsFreshDistribution()
    {
        _snapshots.Setup(x => x.SetAsync(It.IsAny<string>(), It.IsAny<ExpenseDistributionSnapshot>())).ThrowsAsync(new InvalidOperationException());
        var handler = new DistributionHandler();
        using var context = Context(handler);
        var cut = Render(context);
        cut.WaitForAssertion(() => Assert.Equal(1, handler.Count), _wait);
        handler.Complete(0, Items(20));
        cut.WaitForAssertion(() => Assert.Equal(20, View(cut).Data.Single().Value), _wait);
        await Drain(cut);
        VerifyNoSnackbar();
    }

    private void Stored(decimal value) =>
        _snapshots.Setup(x => x.GetAsync<ExpenseDistributionSnapshot>(_key)).ReturnsAsync(new ExpenseDistributionSnapshot
        {
            UserId = 1,
            CurrencyId = DefaultCurrency.PLN.Id,
            StartDate = _start,
            EndDate = _end.AddMinutes(-1),
            Items = Items(value),
            FetchedAtUtc = _start.AddYears(-1),
        });

    private BunitContext Context(DistributionHandler handler)
    {
        var context = new BunitContext();
        context.ComponentFactories.AddStub<ExpenseDistributionOverviewCardView>();
        context.Services.AddLogging();
        context.Services.AddSingleton(_snackbar.Object);
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

    private static IRenderedComponent<ExpenseDistributionOverviewCard> Render(BunitContext context) =>
        context.Render<ExpenseDistributionOverviewCard>(p => p.Add(x => x.StartDateTime, _start).Add(x => x.EndDateTime, _end));

    private static (List<NameValueResult> Data, bool IsLoading, string Currency) View(IRenderedComponent<ExpenseDistributionOverviewCard> cut)
    {
        var stub = cut.FindComponent<Bunit.TestDoubles.Stub<ExpenseDistributionOverviewCardView>>();
        return (stub.Instance.Parameters.Get(x => x.Data), stub.Instance.Parameters.Get(x => x.IsLoading),
            stub.Instance.Parameters.Get(x => x.CurrencyShortName));
    }

    private void VerifyNoSnackbar() =>
        _snackbar.Verify(x => x.Add(It.IsAny<string>(), It.IsAny<Severity>(), It.IsAny<Action<SnackbarOptions>>(), It.IsAny<string>()), Times.Never);

    private static Task Drain(IRenderedComponent<ExpenseDistributionOverviewCard> cut) => cut.InvokeAsync(async () => await Task.Delay(50));
    private static List<NameValueResult> Items(decimal value) => [new("Food", value)];

    private sealed class DistributionHandler : HttpMessageHandler
    {
        private readonly List<TaskCompletionSource<HttpResponseMessage>> _responses = [];
        public int Count => _responses.Count;

        public void Complete(int index, List<NameValueResult>? items) =>
            _responses[index].SetResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(items),
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