using FinanceManager.Application.FinancialAccounts.Stock.Pricing;
using FinanceManager.Application.FinancialAccounts.Stock.Resolution;
using FinanceManager.Domain.Assets.Discovery;
using FinanceManager.Domain.FinancialAccounts.Investments.Dtos;
using FinanceManager.Domain.Identity.Entities;
using FinanceManager.Tests.Integration.Shared;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace FinanceManager.Tests.Integration.Features.FinancialAccounts.Investments.Controllers;

[Trait("Category", "Integration")]
public class InvestmentInstrumentDiscoveryControllerTests(OptionsProvider optionsProvider) : ControllerTests(optionsProvider)
{
    private readonly Mock<IOpenFigiClient> _openFigiMock = new();
    private readonly Mock<IAlphaVantageClient> _alphaVantageMock = new();

    // Override the external provider clients with mocks so the real discovery service runs but no
    // real OpenFIGI/Alpha Vantage calls are made. The last registration for a service type wins,
    // so these replace the typed HttpClient registrations for resolution.
    protected override void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton(_openFigiMock.Object);
        services.AddSingleton(_alphaVantageMock.Object);
    }

    [Fact]
    public async Task Search_WithoutAuthentication_ReturnsUnauthorized()
    {
        var response = await Client.GetAsync("api/investments/instruments/search?query=CSPX", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Search_WithEmptyQuery_ReturnsEmptyWithoutCallingProviders()
    {
        Authorize("testuser", 91, UserRole.User);

        var results = await Client.GetFromJsonAsync<List<InstrumentDiscoveryResultDto>>(
            "api/investments/instruments/search?query=", TestContext.Current.CancellationToken);

        Assert.NotNull(results);
        Assert.Empty(results);
        _openFigiMock.Verify(x => x.MapByTickerAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
        _alphaVantageMock.Verify(x => x.SearchTicker(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Search_WithTicker_ReturnsNormalizedListingResults()
    {
        _openFigiMock
            .Setup(x => x.MapByTickerAsync("CSPX", It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<OpenFigiListing>
            {
                new(Isin: null, Ticker: "CSPX", Name: "iShares Core S&P 500 UCITS ETF", ExchCode: "LN",
                    Currency: "USD", Figi: "BBG001", ShareClassFigi: "BBGSC"),
            });
        _alphaVantageMock
            .Setup(x => x.SearchTicker("CSPX", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TickerSearchMatch>
            {
                new() { Symbol = "CSPX.LON", Name = "iShares Core S&P 500 ETF", Type = "ETF", Region = "United Kingdom", Currency = "USD", MatchScore = 1m },
            });

        Authorize("testuser", 91, UserRole.User);

        var results = await Client.GetFromJsonAsync<List<InstrumentDiscoveryResultDto>>(
            "api/investments/instruments/search?query=CSPX", TestContext.Current.CancellationToken);

        Assert.NotNull(results);
        var result = Assert.Single(results);
        Assert.Equal("Combined", result.Source);
        Assert.Equal("CSPX", result.Ticker);
        Assert.Equal("XLON", result.ExchangeMic);
        Assert.Equal("CSPX.LON", result.ProviderSymbol);
        Assert.Equal("USD", result.TradingCurrency);
    }
}