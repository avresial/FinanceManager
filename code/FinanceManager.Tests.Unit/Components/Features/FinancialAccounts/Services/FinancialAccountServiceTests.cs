using FinanceManager.Components.Features.FinancialAccounts.HttpClients;
using FinanceManager.Components.Features.FinancialAccounts.Services;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.Identity.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Net;
using System.Net.Http.Json;

namespace FinanceManager.Tests.Unit.Components.Features.FinancialAccounts.Services;

[Trait("Category", "Unit")]
public class FinancialAccountServiceTests
{
    private const string _currencyPath = "/api/CurrencyAccount";
    private const string _stockPath = "/api/InvestmentAccount";
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
    public async Task GetAvailableAccounts_ReturnsIndependentCopies()
    {
        var handler = new CountingHandler();
        var (service, _, _) = CreateService(handler);

        var first = await service.GetAvailableAccounts();
        first.Clear();
        var second = await service.GetAvailableAccounts();

        // Mutating a returned map must not corrupt the shared cache handed to other callers.
        Assert.NotEmpty(second);
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
    public async Task GetAvailableAccounts_AfterAddAccount_Refetches()
    {
        var handler = new CountingHandler();
        var (service, _, _) = CreateService(handler);

        await service.GetAvailableAccounts();
        await service.AddAccount(new CurrencyAccount(1, 0, "New account"));
        await service.GetAvailableAccounts();

        Assert.Equal(2, handler.GetCount(_currencyPath));
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

    [Fact]
    public async Task GetAvailableAccounts_ConcurrentCalls_ShareOneFetch()
    {
        var gate = new TaskCompletionSource();
        var handler = new CountingHandler(gate: gate.Task);
        var (service, _, _) = CreateService(handler);

        var first = service.GetAvailableAccounts();
        var second = service.GetAvailableAccounts();
        var names = service.GetAvailableAccountNames();
        gate.SetResult();
        await Task.WhenAll(first, second, names);

        Assert.Equal(1, handler.GetCount(_currencyPath));
        Assert.Equal(1, handler.GetCount(_stockPath));
        Assert.Equal(1, handler.GetCount(_bondPath));
        Assert.Equal(3, (await second).Count);
    }

    [Fact]
    public async Task GetAvailableAccountNames_ReturnsNamesFromAccountLists()
    {
        var handler = new CountingHandler();
        var (service, _, _) = CreateService(handler);

        var names = await service.GetAvailableAccountNames();

        Assert.Equal(_currencyPath, names[1]);
        Assert.Equal(_stockPath, names[2]);
        Assert.Equal(_bondPath, names[3]);
    }

    [Fact]
    public async Task GetAvailableAccountNames_AfterUpdateAccount_Refetches()
    {
        var handler = new CountingHandler();
        var (service, _, _) = CreateService(handler);

        await service.GetAvailableAccountNames();
        await service.UpdateAccount(new CurrencyAccount(1, 1, "Renamed"));
        await service.GetAvailableAccountNames();

        // A rename changes the cached names, so the list must be fetched again.
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
            new InvestmentAccountHttpClient(httpClient),
            new BondAccountHttpClient(httpClient),
            new BondEntryHttpClient(httpClient),
            sync,
            loginService.Object,
            NullLogger<FinancialAccountService>.Instance);

        return (service, sync, loginService);
    }

    private sealed class CountingHandler(bool returnEmpty = false, Task? gate = null) : HttpMessageHandler
    {
        private readonly Dictionary<string, int> _getCounts = new(StringComparer.OrdinalIgnoreCase);

        public int GetCount(string path) => _getCounts.TryGetValue(path, out var count) ? count : 0;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri!.AbsolutePath;

            if (request.Method == HttpMethod.Get)
            {
                _getCounts[path] = GetCount(path) + 1;

                // Holding responses back lets a test issue several calls while the first is still in flight.
                if (gate is not null)
                    await gate;

                object payload = returnEmpty
                    ? Array.Empty<object>()
                    : new[] { new { AccountId = AccountIdFor(path), AccountName = path } };

                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(payload),
                };
            }

            // Add returns the new account id (a number); delete and update return a bool.
            object mutationResult = request.Method == HttpMethod.Post ? 99 : true;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(mutationResult),
            };
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