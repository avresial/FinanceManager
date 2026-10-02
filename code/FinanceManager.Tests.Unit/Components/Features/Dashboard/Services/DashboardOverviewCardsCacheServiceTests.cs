using Blazored.LocalStorage;
using FinanceManager.Components.Features.Dashboard.Models;
using FinanceManager.Components.Features.Dashboard.Services;
using FinanceManager.Components.Features.MoneyFlow.HttpClients;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.MoneyFlow.Entities;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Net;
using System.Net.Http.Json;

namespace FinanceManager.Tests.Unit.Components.Features.Dashboard.Services;

[Trait("Category", "Unit")]
public class DashboardOverviewCardsCacheServiceTests
{
    private static readonly DateTime _start = new(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime _end = _start.AddDays(30).AddHours(12);
    private readonly Mock<ILocalStorageService> _storage = new();
    private readonly ControlledHandler _handler = new();
    private readonly MemoryCache _memory = new(new MemoryCacheOptions());

    [Fact]
    public async Task GetFreshAsync_OverlappingCalls_ShareOneRequestPerSeries()
    {
        var service = Service();
        var first = service.GetFreshAsync(Context());
        var second = service.GetFreshAsync(Context());
        Assert.Equal(1, _handler.CountOf("GetNetCashFlow"));
        Assert.Equal(1, _handler.CountOf("GetClosingBalance"));
        _handler.CompleteAll("GetNetCashFlow", [new(_start, 5)]);
        _handler.CompleteAll("GetClosingBalance", [new(_start, 9)]);
        var a = await first;
        var b = await second;
        Assert.Same(a, b);
        Assert.Equal(5, a.NetCashFlowSeries.Single().Value);
        Assert.Equal(9, a.ClosingBalanceSeries.Single().Value);
    }

    [Fact]
    public async Task GetFreshAsync_AfterCompletion_StartsNewRequest()
    {
        var service = Service();
        var first = service.GetFreshAsync(Context());
        _handler.CompleteAll("GetNetCashFlow", []);
        _handler.CompleteAll("GetClosingBalance", []);
        await first;
        var second = service.GetFreshAsync(Context());
        Assert.Equal(2, _handler.CountOf("GetNetCashFlow"));
        Assert.Equal(2, _handler.CountOf("GetClosingBalance"));
        _handler.CompleteAll("GetNetCashFlow", []);
        _handler.CompleteAll("GetClosingBalance", []);
        await second;
    }

    [Fact]
    public async Task GetFreshAsync_DifferentRange_DoesNotShare()
    {
        var service = Service();
        var first = service.GetFreshAsync(Context());
        var other = Context();
        other.StartDateTime = _start.AddDays(3);
        var second = service.GetFreshAsync(other);
        Assert.Equal(2, _handler.CountOf("GetNetCashFlow"));
        _handler.CompleteAll("GetNetCashFlow", []);
        _handler.CompleteAll("GetClosingBalance", []);
        await Task.WhenAll(first, second);
    }

    [Fact]
    public async Task GetFreshAsync_DoesNotReadOrWriteTtlCache()
    {
        var service = Service();
        var fresh = service.GetFreshAsync(Context());
        _handler.CompleteAll("GetNetCashFlow", []);
        _handler.CompleteAll("GetClosingBalance", []);
        await fresh;
        Assert.Empty(_storage.Invocations);
        Assert.Equal(0, _memory.Count);
    }

    [Fact]
    public async Task GetFreshAsync_PropagatesExceptions_AndAllowsRetry()
    {
        var service = Service();
        var failing = service.GetFreshAsync(Context());
        _handler.FailAll("GetNetCashFlow");
        _handler.CompleteAll("GetClosingBalance", []);
        await Assert.ThrowsAsync<HttpRequestException>(() => failing);
        var retry = service.GetFreshAsync(Context());
        Assert.Equal(2, _handler.CountOf("GetNetCashFlow"));
        _handler.CompleteAll("GetNetCashFlow", []);
        _handler.CompleteAll("GetClosingBalance", []);
        await retry;
    }

    private DashboardOverviewCardsCacheService Service() =>
        new(_storage.Object, _memory,
            new MoneyFlowHttpClient(new HttpClient(_handler) { BaseAddress = new Uri("http://localhost/") }),
            NullLogger<DashboardOverviewCardsCacheService>.Instance);

    private static DashboardOverviewCardsRefreshContext Context() => new()
    {
        UserId = 1,
        CurrencyId = DefaultCurrency.PLN.Id,
        StartDateTime = _start,
        EndDateTime = _end,
    };

    private sealed class ControlledHandler : HttpMessageHandler
    {
        private readonly List<(string Endpoint, TaskCompletionSource<HttpResponseMessage> Response)> _requests = [];

        public int CountOf(string endpoint)
        {
            lock (_requests) return _requests.Count(x => x.Endpoint == endpoint);
        }

        public void CompleteAll(string endpoint, List<TimeSeriesModel> points) => Respond(endpoint, () =>
            new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(points) });

        public void FailAll(string endpoint) => Respond(endpoint, () => new HttpResponseMessage(HttpStatusCode.InternalServerError));

        private void Respond(string endpoint, Func<HttpResponseMessage> create)
        {
            lock (_requests)
            {
                foreach (var (name, response) in _requests.Where(x => x.Endpoint == endpoint))
                    response.TrySetResult(create());
            }
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var endpoint = request.RequestUri!.AbsolutePath.Contains("GetClosingBalance") ? "GetClosingBalance" : "GetNetCashFlow";
            var response = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
            lock (_requests) _requests.Add((endpoint, response));
            return response.Task;
        }
    }
}