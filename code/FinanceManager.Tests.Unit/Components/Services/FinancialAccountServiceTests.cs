using FinanceManager.Components.HttpClients;
using FinanceManager.Components.Services;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.Identity.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Net;
using System.Net.Http.Json;

namespace FinanceManager.Tests.Unit.Components.Services;

[Trait("Category", "Unit")]
public class FinancialAccountServiceTests
{
    private const string _currencyPath = "/api/CurrencyAccount";
    private const string _stockPath = "/api/StockAccount";
    private const string _bondPath = "/api/BondAccount";

    [Fact]
    public async Task GetAvailableAccounts_SecondCall_ServedFromCacheWithoutRefetching()
    {
        var handler = new CountingHandler();
        var (service, _, _) = CreateService(handler);

        var first = await service.GetAvailableAccounts();
        var second = await service.GetAvailableAccounts();

        Assert.Equal(first, second);
        Assert.Equal(1, handler.GetCount(_currencyPath));
        Assert.Equal(1, handler.GetCount(_stockPath));
        Assert.Equal(1, handler.GetCount(_bondPath));
    }

    [Fact]
    public async Task GetAvailableAccounts_AfterAccountsChanged_Refetches()
    {
        var handler = new CountingHandler();
        var (service, sync, _) = CreateService(handler);

        await service.GetAvailableAccounts();
        await sync.AccountChanged();
        await service.GetAvailableAccounts();

        Assert.Equal(2, handler.GetCount(_currencyPath));
        Assert.Equal(2, handler.GetCount(_stockPath));
        Assert.Equal(2, handler.GetCount(_bondPath));
    }

    [Fact]
    public async Task GetAvailableAccounts_AfterLoginStateChanged_Refetches()
    {
        var handler = new CountingHandler();
        var (service, _, loginService) = CreateService(handler);

        await service.GetAvailableAccounts();
        loginService.Raise(s => s.LogginStateChanged += null, false);
        await service.GetAvailableAccounts();

        Assert.Equal(2, handler.GetCount(_currencyPath));
    }

    [Fact]
    public async Task GetAvailableAccounts_AfterRemoveAccount_Refetches()
    {
        var handler = new CountingHandler();
        var (service, _, _) = CreateService(handler);

        await service.GetAvailableAccounts();
        await service.RemoveAccount(1);
        var countAfterRemove = handler.GetCount(_currencyPath);
        await service.GetAvailableAccounts();

        // RemoveAccount itself probes the endpoints (via AccountExists), so the assertion is simply that
        // the post-removal GetAvailableAccounts hit the network again rather than serving a stale cache.
        Assert.True(handler.GetCount(_currencyPath) > countAfterRemove);
    }

    [Fact]
    public async Task GetAvailableAccounts_EmptyResult_IsNotCached()
    {
        var handler = new CountingHandler(returnEmpty: true);
        var (service, _, _) = CreateService(handler);

        await service.GetAvailableAccounts();
        await service.GetAvailableAccounts();

        // An empty map must not be pinned (covers transient failures and the guest mock-seeding flow).
        Assert.Equal(2, handler.GetCount(_currencyPath));
    }

    private static (FinancialAccountService service, AccountDataSynchronizationService sync, Mock<ILoginService> loginService)
        CreateService(CountingHandler handler)
    {
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") };

        var sync = new AccountDataSynchronizationService();
        var loginService = new Mock<ILoginService>();

        var service = new FinancialAccountService(
            new CurrencyAccountHttpClient(httpClient),
            new CurrencyEntryHttpClient(httpClient),
            new StockAccountHttpClient(httpClient),
            new StockEntryHttpClient(httpClient),
            new BondAccountHttpClient(httpClient),
            new BondEntryHttpClient(httpClient),
            sync,
            loginService.Object,
            NullLogger<FinancialAccountService>.Instance);

        return (service, sync, loginService);
    }

    private sealed class CountingHandler(bool returnEmpty = false) : HttpMessageHandler
    {
        private readonly Dictionary<string, int> _getCounts = new(StringComparer.OrdinalIgnoreCase);

        public int GetCount(string path) => _getCounts.TryGetValue(path, out var count) ? count : 0;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri!.AbsolutePath;

            if (request.Method == HttpMethod.Get)
            {
                _getCounts[path] = GetCount(path) + 1;

                object payload = returnEmpty
                    ? Array.Empty<object>()
                    : new[] { new { AccountId = AccountIdFor(path), AccountName = path } };

                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(payload),
                });
            }

            // Mutations (e.g. DELETE in RemoveAccount) simply succeed.
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(true),
            });
        }

        private static int AccountIdFor(string path) => path switch
        {
            _currencyPath => 1,
            _stockPath => 2,
            _bondPath => 3,
            _ => 0,
        };
    }
}