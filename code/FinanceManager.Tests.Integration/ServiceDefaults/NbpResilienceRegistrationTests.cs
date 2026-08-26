using FinanceManager.Application.Shared.Options;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Currencies.Services;
using FinanceManager.Infrastructure;
using FinanceManager.Infrastructure.Features.FinancialAccounts.Currencies.Providers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using ServiceDefaults;
using System.Net;
using Xunit;

namespace FinanceManager.Tests.Integration.ServiceDefaults;

[Trait("Category", "Unit")]
public class NbpResilienceRegistrationTests
{
    private static readonly Currency _usd = new(1, "USD", "$");
    private static readonly Currency _pln = new(0, "PLN", "zł");
    private static readonly DateTime _date = new(2024, 1, 2, 0, 0, 0, DateTimeKind.Utc);

    private const string _usdMidResponse = """
        {"table":"A","currency":"dolar amerykański","code":"USD","rates":[{"no":"1/A/NBP/2024","effectiveDate":"2024-01-02","mid":4.0000}]}
        """;

    [Fact]
    public async Task NbpRegistration_RetriesOnHttp408_UpToConfiguredAttempts()
    {
        var attempts = 0;
        var handler = new StubHandler(_ =>
        {
            attempts++;
            if (attempts < 3)
            {
                return new HttpResponseMessage(HttpStatusCode.RequestTimeout); // 408
            }
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(_usdMidResponse)
            };
        });

        var builder = Host.CreateApplicationBuilder();
        builder.AddServiceDefaults();
        builder.Services.AddInfrastructureApi();
        builder.Services.Configure<NbpOptions>(opt => opt.BaseUrl = "https://api.nbp.pl/api");

        builder.Services.ConfigureHttpClientDefaults(http =>
        {
            http.ConfigurePrimaryHttpMessageHandler(() => handler);
        });

        using var host = builder.Build();
        var providers = host.Services.GetRequiredService<IEnumerable<ICurrencyExchangeRateProvider>>();
        var nbp = providers.OfType<NbpCurrencyExchangeRateProvider>().Single();

        var result = await nbp.GetExchangeRateAsync(_usd, _pln, _date);

        Assert.Equal(CurrencyExchangeRateProviderStatus.Success, result.Status);
        Assert.Equal(4.0m, result.Value);
        Assert.Equal(3, attempts);
    }

    [Fact]
    public async Task NbpRegistration_RetriesOnTransportFailure_UpToConfiguredAttempts()
    {
        var attempts = 0;
        var handler = new StubHandler(_ =>
        {
            attempts++;
            if (attempts < 2)
            {
                throw new HttpRequestException("Simulated network/transport failure");
            }
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(_usdMidResponse)
            };
        });

        var builder = Host.CreateApplicationBuilder();
        builder.AddServiceDefaults();
        builder.Services.AddInfrastructureApi();
        builder.Services.Configure<NbpOptions>(opt => opt.BaseUrl = "https://api.nbp.pl/api");

        builder.Services.ConfigureHttpClientDefaults(http =>
        {
            http.ConfigurePrimaryHttpMessageHandler(() => handler);
        });

        using var host = builder.Build();
        var providers = host.Services.GetRequiredService<IEnumerable<ICurrencyExchangeRateProvider>>();
        var nbp = providers.OfType<NbpCurrencyExchangeRateProvider>().Single();

        var result = await nbp.GetExchangeRateAsync(_usd, _pln, _date);

        Assert.Equal(CurrencyExchangeRateProviderStatus.Success, result.Status);
        Assert.Equal(4.0m, result.Value);
        Assert.Equal(2, attempts);
    }

    [Fact]
    public async Task NbpRegistration_DoesNotDoubleRetry_AttemptsAreBounded()
    {
        var attempts = 0;
        var handler = new StubHandler(_ =>
        {
            attempts++;
            return new HttpResponseMessage(HttpStatusCode.InternalServerError);
        });

        var builder = Host.CreateApplicationBuilder();
        builder.AddServiceDefaults();
        builder.Services.AddInfrastructureApi();
        builder.Services.Configure<NbpOptions>(opt => opt.BaseUrl = "https://api.nbp.pl/api");

        builder.Services.ConfigureHttpClientDefaults(http =>
        {
            http.ConfigurePrimaryHttpMessageHandler(() => handler);
        });

        using var host = builder.Build();
        var providers = host.Services.GetRequiredService<IEnumerable<ICurrencyExchangeRateProvider>>();
        var nbp = providers.OfType<NbpCurrencyExchangeRateProvider>().Single();

        var result = await nbp.GetExchangeRateAsync(_usd, _pln, _date);

        Assert.Equal(CurrencyExchangeRateProviderStatus.Failed, result.Status);
        // The standard resilience pipeline may use three or four total attempts
        // depending on the runtime's default retry policy. Double-retrying would
        // cause multiplicative attempts (for example, 16 instead of a bounded retry set).
        Assert.InRange(attempts, 2, 4);
    }

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(responder(request));
        }
    }
}