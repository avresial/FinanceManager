using FinanceManager.Application.FinancialAccounts.Stock.Pricing;
using FinanceManager.Application.Shared.ExternalServices;
using FinanceManager.Application.Shared.Options;
using FinanceManager.Domain.Assets.Entities;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.Shared.ExternalServices.Entities;
using FinanceManager.Infrastructure;
using FinanceManager.Infrastructure.Features.Assets.Providers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace FinanceManager.Tests.Integration.Features.Assets;

[Trait("Category", "Integration")]
public class TwelveDataRegistrationTests
{
    [Fact]
    public async Task ProviderChain_ResolvesTwelveDataAndUsesMockedHttpResponse()
    {
        var handler = new MockHttpMessageHandler("""
            {
              "values": [{ "datetime": "2024-01-15", "close": "188.25" }],
              "status": "ok"
            }
            """);
        var services = new ServiceCollection()
            .AddLogging()
            .AddOptions();
        services.Configure<TwelveDataOptions>(_ => { });
        services.AddInfrastructureApi();
        services.RemoveAll<IExternalServiceConfigService>();
        services.AddSingleton<IExternalServiceConfigService>(new StubConfigService());
        services.AddHttpClient<TwelveDataClient>().ConfigurePrimaryHttpMessageHandler(() => handler);

        await using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var source = scope.ServiceProvider.GetRequiredService<TwelveDataClient>();
        var chain = scope.ServiceProvider.GetRequiredService<IStockPriceSource>();

        Assert.True(chain.SupportsProviderSelection);
        Assert.Equal(200, chain.GetPriority(MarketDataProvider.TwelveData));

        var result = await source.GetDailySeries(
            "AAPL",
            "US0378331005",
            new DateTime(2024, 1, 1),
            new DateTime(2024, 1, 31),
            new Currency(1, "USD", "$"),
            TestContext.Current.CancellationToken);

        Assert.NotNull(handler.LastRequestUri);
        Assert.Equal(188.25m, Assert.Single(result).PricePerUnit);
    }

    private sealed class StubConfigService : IExternalServiceConfigService
    {
        private static readonly ExternalServiceConfiguration _config = new()
        {
            ServiceName = "TwelveData",
            BaseUrl = "https://api.twelvedata.com",
            ApiKey = "test-key",
            IsEnabled = true
        };

        public ValueTask<ExternalServiceConfiguration> GetServiceAsync(string serviceName, CancellationToken ct = default) =>
            ValueTask.FromResult(_config);

        public ValueTask<IReadOnlyList<ExternalServiceConfiguration>> GetAllServicesAsync(CancellationToken ct = default) =>
            ValueTask.FromResult<IReadOnlyList<ExternalServiceConfiguration>>([_config]);

        public Task SaveServiceAsync(ExternalServiceConfiguration config, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class MockHttpMessageHandler(string response) : HttpMessageHandler
    {
        public Uri? LastRequestUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequestUri = request.RequestUri;
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new StringContent(response) });
        }
    }
}