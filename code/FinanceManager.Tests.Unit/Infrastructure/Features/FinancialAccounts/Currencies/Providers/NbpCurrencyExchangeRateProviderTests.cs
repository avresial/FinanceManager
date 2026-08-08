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
public class NbpCurrencyExchangeRateProviderTests
{
    private static readonly Currency _pln = new(0, "PLN", "zł");
    private static readonly Currency _usd = new(1, "USD", "$");
    private static readonly Currency _eur = new(2, "EUR", "€");
    private static readonly DateTime _date = new(2024, 1, 2, 0, 0, 0, DateTimeKind.Utc);

    private const string _usdMidResponse = """
        {"table":"A","currency":"dolar amerykański","code":"USD","rates":[{"no":"1/A/NBP/2024","effectiveDate":"2024-01-02","mid":4.0000}]}
        """;

    private const string _usdRangeResponse = """
        {"table":"A","currency":"dolar amerykański","code":"USD","rates":[{"no":"1/A/NBP/2024","effectiveDate":"2024-01-02","mid":4.0000},{"no":"2/A/NBP/2024","effectiveDate":"2024-01-03","mid":4.1000}]}
        """;

    private static NbpCurrencyExchangeRateProvider CreateProvider(MockHttpMessageHandler handler, bool enabled = true) =>
        new(new HttpClient(handler),
            Options.Create(new NbpOptions { BaseUrl = "https://api.nbp.pl/api", Enabled = enabled }),
            NullLogger<NbpCurrencyExchangeRateProvider>.Instance);

    [Fact]
    public async Task ForeignToPln_MapsMidDirectly()
    {
        var provider = CreateProvider(new MockHttpMessageHandler(_ => Ok(_usdMidResponse)));

        var result = await provider.GetExchangeRateAsync(_usd, _pln, _date);

        Assert.Equal(CurrencyExchangeRateProviderStatus.Success, result.Status);
        Assert.Equal(4.0m, result.Value);
    }

    [Fact]
    public async Task PlnToForeign_InvertsMid()
    {
        var provider = CreateProvider(new MockHttpMessageHandler(_ => Ok(_usdMidResponse)));

        var result = await provider.GetExchangeRateAsync(_pln, _usd, _date);

        Assert.Equal(CurrencyExchangeRateProviderStatus.Success, result.Status);
        Assert.Equal(0.25m, result.Value);
    }

    [Fact]
    public async Task NonPublicationDay_ReturnsNotFound()
    {
        var provider = CreateProvider(new MockHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound)
        {
            Content = new StringContent("404 NotFound - Not Found - Brak danych")
        }));

        var result = await provider.GetExchangeRateAsync(_usd, _pln, _date);

        Assert.Equal(CurrencyExchangeRateProviderStatus.NotFound, result.Status);
    }

    [Fact]
    public async Task PairWithoutPlnLeg_ReturnsNotFound_WithoutCallingApi()
    {
        var handler = new MockHttpMessageHandler(_ => Ok(_usdMidResponse));
        var provider = CreateProvider(handler);

        var result = await provider.GetExchangeRateAsync(_usd, _eur, _date);

        Assert.Equal(CurrencyExchangeRateProviderStatus.NotFound, result.Status);
        Assert.Equal(0, handler.CallCount);
    }

    [Fact]
    public async Task Disabled_ReturnsNotFound_WithoutCallingApi()
    {
        var handler = new MockHttpMessageHandler(_ => Ok(_usdMidResponse));
        var provider = CreateProvider(handler, enabled: false);

        var result = await provider.GetExchangeRateAsync(_usd, _pln, _date);

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

        var result = await provider.GetExchangeRateAsync(_usd, _pln, _date);

        Assert.Equal(CurrencyExchangeRateProviderStatus.Failed, result.Status);
    }

    [Fact]
    public async Task Range_ForeignToPln_MapsEachDay()
    {
        var provider = CreateProvider(new MockHttpMessageHandler(_ => Ok(_usdRangeResponse)));

        var results = await provider.GetExchangeRateAsync(_usd, _pln, _date, _date.AddDays(1));

        Assert.Equal(2, results.Count);
        Assert.Equal(4.0m, results[0].Result.Value);
        Assert.Equal(4.1m, results[1].Result.Value);
    }

    [Fact]
    public async Task Range_ServerError_MarksEveryAffectedDayAsFailed()
    {
        var provider = CreateProvider(new MockHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError)
        {
            Content = new StringContent("boom")
        }));

        var results = await provider.GetExchangeRateAsync(_usd, _pln, _date, _date.AddDays(1));

        Assert.Equal(2, results.Count);
        Assert.All(results, result => Assert.Equal(CurrencyExchangeRateProviderStatus.Failed, result.Result.Status));
    }

    [Fact]
    public async Task Range_Disabled_PlnToPln_ReturnsNotFound_WithoutCallingApi()
    {
        var handler = new MockHttpMessageHandler(_ => Ok(_usdRangeResponse));
        var provider = CreateProvider(handler, enabled: false);

        var results = await provider.GetExchangeRateAsync(_pln, _pln, _date, _date.AddDays(1));

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