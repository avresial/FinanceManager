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
using System.Net;
using System.Net.Http.Json;

namespace FinanceManager.Tests.Unit.Components.Features.Dashboard.Components.Cards.Assets;

[Trait("Category", "Unit")]
public class AssetsDistributionOverviewCardTests
{
    private static readonly DateTime _start = new(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime _end = _start.AddDays(30).AddHours(12);
    private static readonly string _key = $"assets-distribution:1:{DefaultCurrency.PLN.Id}";
    private readonly Mock<ISnapshotService> _snapshots = new();
    private int _userId = 1;
    private Currency _currency = DefaultCurrency.PLN;

    [Fact]
    public async Task HydratesBeforeRequestCompletes_AndFetchesOnEveryVisit()
    {
        Stored(10);
        var handler = new DistributionHandler();
        using var context = Context(handler);
        var first = Render(context);
        first.WaitForAssertion(() => Assert.Equal(10, View(first).TypeData.Single().Value));
        Assert.Equal(1, handler.Count);
        Assert.Equal("PLN", View(first).Currency);
        Assert.Equal("300px", View(first).Height);
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
    public async Task EqualDistribution_DoesNotWrite_DespiteDifferentCaptureTime()
    {
        Stored(10);
        var handler = new DistributionHandler();
        using var context = Context(handler);
        var cut = Render(context);
        handler.Complete(0, Points(10));
        await Drain(cut);
        _snapshots.Verify(x => x.SetAsync(It.IsAny<string>(), It.IsAny<AssetsDistributionSnapshot>()), Times.Never);
    }

    [Fact]
    public async Task EmptySuccess_ReplacesPopulatedSnapshot()
    {
        Stored(10);
        var handler = new DistributionHandler();
        using var context = Context(handler);
        var cut = Render(context);
        handler.Complete(0, []);
        cut.WaitForAssertion(() => Assert.Empty(View(cut).TypeData));
        await Drain(cut);
        _snapshots.Verify(x => x.SetAsync(_key, It.Is<AssetsDistributionSnapshot>(s => s.TypeData.Count == 0)), Times.Once);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, false)]
    [InlineData(true, true)]
    [InlineData(false, true)]
    public async Task FailedRefresh_PreservesSnapshotOrShowsBlockingError(bool stored, bool accountFails)
    {
        if (stored) Stored(10);
        var handler = new DistributionHandler();
        using var context = Context(handler);
        var cut = Render(context);
        handler.Fail(0, accountFails);
        cut.WaitForAssertion(() =>
        {
            Assert.False(View(cut).IsLoading);
            Assert.Equal(!stored, View(cut).HasError);
            if (stored) Assert.Equal(10, View(cut).TypeData.Single().Value);
            else Assert.Empty(View(cut).TypeData);
        });
        _snapshots.Verify(x => x.SetAsync(It.IsAny<string>(), It.IsAny<AssetsDistributionSnapshot>()), Times.Never);
        await Drain(cut);
    }

    [Fact]
    public async Task SameScopeRefreshFailure_PreservesPreviouslyDisplayedModel_WhenSnapshotIsMissing()
    {
        var handler = new DistributionHandler();
        using var context = Context(handler);
        var cut = Render(context);
        handler.Complete(0, Points(10));
        await Drain(cut);

        cut.Render(parameters => parameters.Add(x => x.Height, "301px"));
        cut.WaitForAssertion(() => Assert.Equal(2, handler.Count));
        Assert.Equal(10, View(cut).TypeData.Single().Value);
        handler.Fail(1, accountFails: false);
        await Drain(cut);

        Assert.Equal(10, View(cut).TypeData.Single().Value);
        Assert.False(View(cut).HasError);
        Assert.False(View(cut).IsLoading);
    }

    [Fact]
    public async Task UserChange_ClearsPreviousUsersModelBeforeSnapshotReadCompletes()
    {
        var handler = new DistributionHandler();
        using var context = Context(handler);
        var cut = Render(context);
        handler.Complete(0, Points(10));
        await Drain(cut);

        var read = new TaskCompletionSource<AssetsDistributionSnapshot?>(TaskCreationOptions.RunContinuationsAsynchronously);
        _snapshots.Setup(x => x.GetAsync<AssetsDistributionSnapshot>("assets-distribution:2:0")).Returns(read.Task);
        _userId = 2;
        cut.Render(parameters => parameters.Add(x => x.Height, "301px"));

        Assert.True(View(cut).IsLoading);
        Assert.Empty(View(cut).TypeData);
        Assert.Equal("PLN", View(cut).Currency);
        read.SetResult(null);
        cut.WaitForAssertion(() => Assert.Equal(2, handler.Count));
        handler.Complete(1, Points(20));
        await Drain(cut);
        Assert.Equal(20, View(cut).TypeData.Single().Value);
    }

    [Fact]
    public async Task CurrencyChange_UpdatesLabelAndClearsPreviousCurrencyBeforeSnapshotReadCompletes()
    {
        var handler = new DistributionHandler();
        using var context = Context(handler);
        var cut = Render(context);
        handler.Complete(0, Points(10));
        await Drain(cut);

        var read = new TaskCompletionSource<AssetsDistributionSnapshot?>(TaskCreationOptions.RunContinuationsAsynchronously);
        _snapshots.Setup(x => x.GetAsync<AssetsDistributionSnapshot>("assets-distribution:1:1")).Returns(read.Task);
        _currency = DefaultCurrency.USD;
        cut.Render(parameters => parameters.Add(x => x.Height, "301px"));

        Assert.True(View(cut).IsLoading);
        Assert.Empty(View(cut).TypeData);
        Assert.Equal("USD", View(cut).Currency);
        read.SetResult(null);
        cut.WaitForAssertion(() => Assert.Equal(2, handler.Count));
        handler.Complete(1, Points(20));
        await Drain(cut);
        Assert.Equal(20, View(cut).TypeData.Single().Value);
    }

    [Fact]
    public async Task RangeChange_SupersedesSlowRun_AndKeepsStableKeyWithActualDates()
    {
        var handler = new DistributionHandler();
        using var context = Context(handler);
        var cut = Render(context);
        var nextStart = _start.AddDays(5);
        var nextEnd = _end.AddDays(-1);
        cut.Render(parameters => parameters.Add(x => x.StartDateTime, nextStart).Add(x => x.EndDateTime, nextEnd));
        cut.WaitForAssertion(() => Assert.Equal(2, handler.Count));
        handler.Complete(1, [new NameValueResult("Cash", 20)]);
        cut.WaitForAssertion(() => Assert.Equal(20, View(cut).TypeData.Single().Value));
        handler.Complete(0, Points(10));
        await Drain(cut);
        Assert.Equal(20, View(cut).TypeData.Single().Value);
        _snapshots.Verify(x => x.SetAsync(_key, It.Is<AssetsDistributionSnapshot>(s => s.StartDate == nextStart && s.AsOfDate == nextEnd)), Times.Once);
        _snapshots.Verify(x => x.SetAsync(It.IsAny<string>(), It.Is<AssetsDistributionSnapshot>(s => s.StartDate == _start)), Times.Never);
    }

    [Fact]
    public async Task DisposedCard_DoesNotApplyOrPersistPendingRefresh()
    {
        var handler = new DistributionHandler();
        using var context = Context(handler);
        var cut = Render(context);
        cut.WaitForAssertion(() => Assert.Equal(1, handler.Count));

        await context.DisposeComponentsAsync();
        handler.Complete(0, Points(10));
        await Task.Delay(50, Xunit.TestContext.Current.CancellationToken);

        _snapshots.Verify(x => x.SetAsync(It.IsAny<string>(), It.IsAny<AssetsDistributionSnapshot>()), Times.Never);
    }

    [Fact]
    public async Task DifferentStoredRange_IsNotPainted()
    {
        Stored(10, _start.AddDays(-20));
        var handler = new DistributionHandler();
        using var context = Context(handler);
        var cut = Render(context);
        Assert.Empty(View(cut).TypeData);
        Assert.True(View(cut).IsLoading);
        handler.Complete(0, Points(20));
        cut.WaitForAssertion(() => Assert.Equal(20, View(cut).TypeData.Single().Value));
        await Drain(cut);
    }

    [Fact]
    public async Task StorageReadFailure_StillFetchesAndRenders()
    {
        _snapshots.Setup(x => x.GetAsync<AssetsDistributionSnapshot>(_key)).ThrowsAsync(new InvalidOperationException());
        var handler = new DistributionHandler();
        using var context = Context(handler);
        var cut = Render(context);
        handler.Complete(0, Points(20));
        cut.WaitForAssertion(() => Assert.Equal(20, View(cut).TypeData.Single().Value));
        _snapshots.Verify(x => x.RemoveAsync(_key), Times.Once);
        await Drain(cut);
    }

    [Fact]
    public async Task StorageWriteFailure_KeepsFreshChart()
    {
        _snapshots.Setup(x => x.SetAsync(It.IsAny<string>(), It.IsAny<AssetsDistributionSnapshot>())).ThrowsAsync(new InvalidOperationException());
        var handler = new DistributionHandler();
        using var context = Context(handler);
        var cut = Render(context);
        handler.Complete(0, Points(20));
        cut.WaitForAssertion(() => Assert.Equal(20, View(cut).TypeData.Single().Value));
        await Drain(cut);
        Assert.False(View(cut).HasError);
    }

    [Fact]
    public async Task SharesDistributionRequestWithAssetsCache_AndSnapshotWriteIsIndependent()
    {
        var handler = new DistributionHandler();
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
        Assert.Equal(1, handler.AccountCount);
        handler.Complete(0, Points(20));
        await aggregate;
        cut.WaitForAssertion(() => Assert.Equal(20, View(cut).TypeData.Single().Value));
        await Drain(cut);
        _snapshots.Verify(x => x.SetAsync(_key, It.IsAny<AssetsDistributionSnapshot>()), Times.Once);
        Assert.Single(_snapshots.Invocations, x => x.Method.Name == nameof(ISnapshotService.SetAsync));
    }

    [Fact]
    public async Task PreparedDashboardModel_DoesNotFetchOrReadSnapshot_AndSupersedesFallback()
    {
        var handler = new DistributionHandler();
        using var context = Context(handler);
        var cut = Render(context);
        cut.Render(p => p.Add(x => x.Model, new DistributionCardModel(Points(99), Points(99))));
        cut.WaitForAssertion(() => Assert.Equal(99, View(cut).TypeData.Single().Value));
        handler.Complete(0, Points(10));
        await Drain(cut);
        Assert.Equal(99, View(cut).TypeData.Single().Value);
        Assert.Equal(1, handler.Count);
        _snapshots.Verify(x => x.SetAsync(It.IsAny<string>(), It.IsAny<AssetsDistributionSnapshot>()), Times.Never);
        cut.Dispose();
        _snapshots.Invocations.Clear();
        var supplied = context.Render<AssetsDistributionOverviewCard>(p => p.Add(x => x.Model, new DistributionCardModel(Points(50), [])));
        Assert.Equal(50, View(supplied).TypeData.Single().Value);
        Assert.Empty(_snapshots.Invocations);
        Assert.Equal(1, handler.Count);
    }

    [Fact]
    public async Task ChangedWalletOnly_UpdatesBothSnapshotLists()
    {
        Stored(10);
        var handler = new DistributionHandler();
        using var context = Context(handler);
        var cut = Render(context);
        handler.Complete(0, Points(10), Points(25));
        await Drain(cut);
        _snapshots.Verify(x => x.SetAsync(_key, It.Is<AssetsDistributionSnapshot>(s => s.TypeData.Single().Value == 10 && s.AccountData.Single().Value == 25)), Times.Once);
    }

    [Fact]
    public async Task MissingResponse_PreservesPaintedSnapshot()
    {
        Stored(10);
        var handler = new DistributionHandler();
        using var context = Context(handler);
        var cut = Render(context);
        handler.Complete(0, null);
        await Drain(cut);
        Assert.Equal(10, View(cut).TypeData.Single().Value);
        _snapshots.Verify(x => x.SetAsync(It.IsAny<string>(), It.IsAny<AssetsDistributionSnapshot>()), Times.Never);
    }

    [Theory]
    [InlineData(2, 0)]
    [InlineData(1, 3)]
    public async Task DifferentStoredScope_IsNotPainted(int userId, int currencyId)
    {
        _snapshots.Setup(x => x.GetAsync<AssetsDistributionSnapshot>(_key)).ReturnsAsync(new AssetsDistributionSnapshot
        {
            UserId = userId,
            CurrencyId = currencyId,
            StartDate = _start,
            AsOfDate = _end,
            TypeData = Points(99),
        });
        var handler = new DistributionHandler();
        using var context = Context(handler);
        var cut = Render(context);
        Assert.True(View(cut).IsLoading);
        Assert.Empty(View(cut).TypeData);
        handler.Complete(0, Points(20));
        await Drain(cut);
        Assert.Equal(20, View(cut).TypeData.Single().Value);
    }

    private void Stored(decimal value, DateTime? start = null) =>
        _snapshots.Setup(x => x.GetAsync<AssetsDistributionSnapshot>(_key)).ReturnsAsync(new AssetsDistributionSnapshot
        {
            UserId = 1,
            CurrencyId = DefaultCurrency.PLN.Id,
            StartDate = start ?? _start,
            AsOfDate = _end.AddMinutes(-1),
            TypeData = Points(value),
            AccountData = Points(value),
            FetchedAtUtc = _start.AddYears(-1),
        });

    private BunitContext Context(DistributionHandler handler)
    {
        var context = new BunitContext();
        context.ComponentFactories.AddStub<AssetsDistributionOverviewCardView>();
        context.Services.AddLogging();
        var login = new Mock<ILoginService>();
        login.Setup(x => x.GetLoggedUser()).ReturnsAsync(() => new UserSession { UserId = _userId, UserName = "tester", Password = "", UserRole = UserRole.User });
        var settings = new Mock<ISettingsService>();
        settings.Setup(x => x.GetCurrencyAsync()).ReturnsAsync(() => _currency);
        context.Services.AddSingleton(login.Object);
        context.Services.AddSingleton(settings.Object);
        context.Services.AddSingleton<ISnapshotRefreshCoordinator>(new SnapshotRefreshCoordinator(_snapshots.Object, NullLogger<SnapshotRefreshCoordinator>.Instance));
        context.Services.AddSingleton(new AssetsPageCardsCacheService(Mock.Of<ILocalStorageService>(), new MemoryCache(new MemoryCacheOptions()),
            new AssetsHttpClient(new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") }), NullLogger<AssetsPageCardsCacheService>.Instance));
        return context;
    }

    private static IRenderedComponent<AssetsDistributionOverviewCard> Render(BunitContext context) =>
        context.Render<AssetsDistributionOverviewCard>(p => p.Add(x => x.StartDateTime, _start).Add(x => x.EndDateTime, _end));

    private static (List<NameValueResult> TypeData, bool IsLoading, bool HasError, string Currency, string Height) View(IRenderedComponent<AssetsDistributionOverviewCard> cut)
    {
        var stub = cut.FindComponent<Bunit.TestDoubles.Stub<AssetsDistributionOverviewCardView>>();
        return (stub.Instance.Parameters.Get(x => x.TypeData), stub.Instance.Parameters.Get(x => x.IsLoading),
            stub.Instance.Parameters.Get(x => x.HasError), stub.Instance.Parameters.Get(x => x.CurrencyShortName),
            stub.Instance.Parameters.Get(x => x.Height));
    }

    private static Task Drain(IRenderedComponent<AssetsDistributionOverviewCard> cut) => cut.InvokeAsync(async () => await Task.Delay(50));
    private static List<NameValueResult> Points(decimal value) => [new("Cash", value)];

    private sealed class DistributionHandler : HttpMessageHandler
    {
        private readonly List<TaskCompletionSource<HttpResponseMessage>> _responses = [];
        private readonly List<TaskCompletionSource<HttpResponseMessage>> _accountResponses = [];
        public int AccountCount => _accountResponses.Count;
        public int Count => _responses.Count;
        public void Complete(int index, List<NameValueResult>? points, List<NameValueResult>? accounts = null)
        {
            _responses[index].SetResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(points) });
            _accountResponses[index].SetResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(accounts ?? points) });
        }
        public void Fail(int index, bool accountFails)
        {
            _responses[index].SetResult(new HttpResponseMessage(accountFails ? HttpStatusCode.OK : HttpStatusCode.InternalServerError) { Content = JsonContent.Create(Points(10)) });
            _accountResponses[index].SetResult(new HttpResponseMessage(accountFails ? HttpStatusCode.InternalServerError : HttpStatusCode.OK) { Content = JsonContent.Create(Points(10)) });
        }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri!.AbsolutePath;
            if (path.Contains("GetEndAssetsPerType", StringComparison.Ordinal) || path.Contains("GetEndAssetsPerAccount", StringComparison.Ordinal))
            {
                var response = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
                (path.Contains("GetEndAssetsPerType", StringComparison.Ordinal) ? _responses : _accountResponses).Add(response);
                return response.Task;
            }
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(Array.Empty<object>()) });
        }
    }
}