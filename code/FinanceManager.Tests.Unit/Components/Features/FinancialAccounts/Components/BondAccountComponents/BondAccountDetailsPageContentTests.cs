using FinanceManager.Components.Features.FinancialAccounts.Components.BondAccountComponents.TransactionHistory;
using FinanceManager.Components.Features.FinancialAccounts.HttpClients;
using FinanceManager.Components.Features.FinancialAccounts.Services;
using FinanceManager.Domain.FinancialAccounts.Bond.Entities;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.Identity.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Collections.Concurrent;
using System.Net;
using System.Text;

namespace FinanceManager.Tests.Unit.Components.Features.FinancialAccounts.Components.BondAccountComponents;

[Trait("Category", "Unit")]
public class BondAccountDetailsPageContentTests
{
    [Fact]
    public async Task UpdateInfo_OverlappingLoadsRequestEachBondDetailsOnlyOnce()
    {
        var handler = new BlockingBondDetailsHandler([1, 2]);
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") };
        var settings = new Mock<ISettingsService>();
        settings.Setup(x => x.GetCurrencyAsync()).ReturnsAsync(DefaultCurrency.PLN);

#pragma warning disable BL0005
        var component = new BondAccountDetailsPageContent
        {
            AccountId = 10,
            Account = new BondAccount(1, 10, "Bonds", [
                new BondAccountEntry(10, 1, DateTime.UtcNow, 0, 1, 1),
                new BondAccountEntry(10, 2, DateTime.UtcNow, 0, 1, 2)]),
            SettingsService = settings.Object,
            BondDetailsHttpClient = new BondDetailsHttpClient(httpClient),
            FinancialAccountService = null!,
            AccountDataSynchronizationService = null!,
            LoginService = null!,
            MoneyFlowHttpClient = null!,
            SnapshotStore = null!,
            ChartSnapshotStore = null!,
            Logger = NullLogger<BondAccountDetailsPageContent>.Instance,
            BrowserViewportService = null!,
        };
#pragma warning restore BL0005

        var firstLoad = component.UpdateInfo(refreshChart: false);
        await handler.AllRequestsStarted.Task.WaitAsync(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);
        var overlappingLoad = component.UpdateInfo(refreshChart: false);

        handler.Release();
        await Task.WhenAll(firstLoad, overlappingLoad);

        Assert.Equal(1, handler.CountFor(1));
        Assert.Equal(1, handler.CountFor(2));
    }

    private sealed class BlockingBondDetailsHandler(IReadOnlyCollection<int> expectedIds) : HttpMessageHandler
    {
        private readonly TaskCompletionSource<bool> _release =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly ConcurrentDictionary<int, int> _requestCounts = new();

        public TaskCompletionSource<bool> AllRequestsStarted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var bondDetailsId = int.Parse(request.RequestUri!.Segments[^1]);
            _requestCounts.AddOrUpdate(bondDetailsId, 1, (_, count) => count + 1);
            if (_requestCounts.Keys.Intersect(expectedIds).Count() == expectedIds.Count)
                AllRequestsStarted.TrySetResult(true);

            await _release.Task.WaitAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("null", Encoding.UTF8, "application/json"),
            };
        }

        public int CountFor(int bondDetailsId) => _requestCounts.GetValueOrDefault(bondDetailsId);

        public void Release() => _release.TrySetResult(true);
    }
}