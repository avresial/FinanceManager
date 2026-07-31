using FinanceManager.Application.Shared.Options;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Currencies.Services;
using FinanceManager.Infrastructure.Features.FinancialAccounts.Currencies.Providers;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System.Net;

namespace FinanceManager.Tests.Unit.Infrastructure.Features.FinancialAccounts.Currencies.Providers;

[Collection("Infrastructure")]
[Trait("Category", "Unit")]
public class EcbCurrencyExchangeRateProviderTests
{
    private static readonly Currency _eur = new(2, "EUR", "€");
    private static readonly Currency _usd = new(1, "USD", "$");
    private static readonly Currency _gbp = new(3, "GBP", "£");
    private static readonly DateTime _date = new(2024, 1, 2, 0, 0, 0, DateTimeKind.Utc);

    private static string Csv(string code, string value) => $"""
        KEY,FREQ,CURRENCY,CURRENCY_DENOM,EXR_TYPE,EXR_SUFFIX,TIME_PERIOD,OBS_VALUE,OBS_STATUS
        EXR.D.{code}.EUR.SP00.A,D,{code},EUR,SP00,A,2024-01-02,{value},A
        """;

    private static EcbCurrencyExchangeRateProvider CreateProvider(MockHttpMessageHandler handler, bool enabled = true) =>
        new(new HttpClient(handler),
            Options.Create(new EcbOptions { BaseUrl = "https://data-api.ecb.europa.eu/service", Enabled = enabled }),
            NullLogger<EcbCurrencyExchangeRateProvider>.Instance);

    [Fact]
    public async Task EurToForeign_UsesObservationDirectly()
    {
        var provider = CreateProvider(new MockHttpMessageHandler(_ => Ok(Csv("USD", "1.1000"))));

        var result = await provider.GetExchangeRateAsync(_eur, _usd, _date);

        Assert.Equal(CurrencyExchangeRateProviderStatus.Success, result.Status);
        Assert.Equal(1.1000m, result.Value);
    }

    [Fact]
    public async Task ForeignToEur_InvertsObservation()
    {
        var provider = CreateProvider(new MockHttpMessageHandler(_ => Ok(Csv("USD", "1.2500"))));

        var result = await provider.GetExchangeRateAsync(_usd, _eur, _date);

        Assert.Equal(CurrencyExchangeRateProviderStatus.Success, result.Status);
        Assert.Equal(1m / 1.2500m, result.Value);
    }

    [Fact]
    public async Task NonEurPair_ComputesCrossRateViaEur()
    {
        // USD is 1.10 per EUR, GBP is 0.85 per EUR → USD→GBP = 0.85 / 1.10.
        var provider = CreateProvider(new MockHttpMessageHandler(request =>
            request.RequestUri!.AbsoluteUri.Contains("D.USD.EUR", StringComparison.Ordinal)
                ? Ok(Csv("USD", "1.1000"))
                : Ok(Csv("GBP", "0.8500"))));

        var result = await provider.GetExchangeRateAsync(_usd, _gbp, _date);

        Assert.Equal(CurrencyExchangeRateProviderStatus.Success, result.Status);
        Assert.Equal(0.8500m / 1.1000m, result.Value);
    }

    [Fact]
    public async Task SameCurrency_ReturnsOne_WithoutCallingApi()
    {
        var handler = new MockHttpMessageHandler(_ => Ok(Csv("USD", "1.1")));
        var provider = CreateProvider(handler);

        var result = await provider.GetExchangeRateAsync(_eur, _eur, _date);

        Assert.Equal(CurrencyExchangeRateProviderStatus.Success, result.Status);
        Assert.Equal(1m, result.Value);
        Assert.Equal(0, handler.CallCount);
    }

    [Fact]
    public async Task UnsupportedCurrency_ReturnsNotFound()
    {
        var provider = CreateProvider(new MockHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound)
        {
            Content = new StringContent("No results found.")
        }));

        var result = await provider.GetExchangeRateAsync(_eur, _usd, _date);

        Assert.Equal(CurrencyExchangeRateProviderStatus.NotFound, result.Status);
    }

    [Fact]
    public async Task Disabled_ReturnsNotFound_WithoutCallingApi()
    {
        var handler = new MockHttpMessageHandler(_ => Ok(Csv("USD", "1.1")));
        var provider = CreateProvider(handler, enabled: false);

        var result = await provider.GetExchangeRateAsync(_eur, _usd, _date);

        Assert.Equal(CurrencyExchangeRateProviderStatus.NotFound, result.Status);
        Assert.Equal(0, handler.CallCount);
    }

    [Fact]
    public async Task ServerError_IsReportedAsFailed()
    {
        var provider = CreateProvider(new MockHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError)
        {
            Content = new StringContent("boom")
        }));

        var result = await provider.GetExchangeRateAsync(_eur, _usd, _date);

        Assert.Equal(CurrencyExchangeRateProviderStatus.Failed, result.Status);
    }

    [Fact]
    public async Task Range_CrossRate_ComputesViaEur()
    {
        var provider = CreateProvider(new MockHttpMessageHandler(request =>
            request.RequestUri!.AbsoluteUri.Contains("D.USD.EUR", StringComparison.Ordinal)
                ? Ok(Csv("USD", "1.1000"))
                : Ok(Csv("GBP", "0.8500"))));

        var results = await provider.GetExchangeRateAsync(_usd, _gbp, _date, _date);

        Assert.Single(results);
        Assert.Equal(CurrencyExchangeRateProviderStatus.Success, results[0].Result.Status);
        Assert.Equal(0.8500m / 1.1000m, results[0].Result.Value);
    }

    [Fact]
    public async Task Range_ServerError_ReportsFailedForEveryDate()
    {
        var provider = CreateProvider(new MockHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError)
        {
            Content = new StringContent("boom")
        }));

        var results = await provider.GetExchangeRateAsync(_eur, _usd, _date, _date.AddDays(1));

        Assert.Equal(2, results.Count);
        Assert.All(results, r => Assert.Equal(CurrencyExchangeRateProviderStatus.Failed, r.Result.Status));
    }

    [Fact]
    public async Task Range_Disabled_ReturnsNotFound_WithoutCallingApi()
    {
        var handler = new MockHttpMessageHandler(_ => Ok(Csv("USD", "1.1")));
        var provider = CreateProvider(handler, enabled: false);

        var results = await provider.GetExchangeRateAsync(_eur, _usd, _date, _date.AddDays(1));

        Assert.Equal(2, results.Count);
        Assert.All(results, r => Assert.Equal(CurrencyExchangeRateProviderStatus.NotFound, r.Result.Status));
        Assert.Equal(0, handler.CallCount);
    }

    private static HttpResponseMessage Ok(string body) =>
        new(HttpStatusCode.OK) { Content = new StringContent(body) };

    private sealed class MockHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        public int CallCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromResult(responder(request));
        }
    }
}