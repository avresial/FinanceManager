using FinanceManager.Application.Shared.ExternalServices;
using FinanceManager.Domain.Shared.ExternalServices.Entities;
using FinanceManager.Infrastructure.Features.Assets.Providers;
using Microsoft.Extensions.Logging;
using System.Net;

namespace FinanceManager.Tests.Unit.Infrastructure.Features.Assets.Providers;

[Collection("Infrastructure")]
[Trait("Category", "Unit")]
public class OpenFigiClientTests
{
    private static IExternalServiceConfigService CreateConfigService(string apiKey = "")
    {
        var config = new ExternalServiceConfiguration
        {
            ServiceName = "OpenFigi",
            BaseUrl = "https://api.openfigi.com/v3",
            ApiKey = apiKey,
            IsEnabled = true,
        };
        return new StubExternalServiceConfigService(config);
    }

    private OpenFigiClient CreateClient(MockHttpMessageHandler handler, string apiKey = "")
    {
        var httpClient = new HttpClient(handler);
        var logger = LoggerFactory.Create(b => { }).CreateLogger<OpenFigiClient>();
        return new OpenFigiClient(httpClient, logger, CreateConfigService(apiKey));
    }

    [Fact]
    public async Task MapByTickerAsync_WithValidTickerAndExchange_ReturnsListings()
    {
        // Arrange — a realistic OpenFIGI v3 mapping payload: matches live inside each job's "data"
        // array, use camelCase FIGI fields, and carry no currency field at all.
        var httpHandler = new MockHttpMessageHandler(response: """
            [
              {
                "data": [
                  {
                    "figi": "BBG000B9XRY4",
                    "name": "Apple Inc",
                    "ticker": "AAPL",
                    "exchCode": "US",
                    "compositeFIGI": "BBG000HKWL63",
                    "shareClassFIGI": "BBG001S5N8V8",
                    "securityType": "Common Stock",
                    "securityType2": "Common Stock",
                    "marketSector": "Equity",
                    "securityDescription": "AAPL"
                  }
                ]
              }
            ]
            """);

        var client = CreateClient(httpHandler);

        // Act
        var result = await client.MapByTickerAsync("AAPL", "US", TestContext.Current.CancellationToken);

        // Assert — no ISIN on the ticker path; FIGIs and security metadata are projected.
        Assert.Single(result);
        Assert.Null(result[0].Isin);
        Assert.Equal("BBG000B9XRY4", result[0].Figi);
        Assert.Equal("BBG000HKWL63", result[0].CompositeFigi);
        Assert.Equal("BBG001S5N8V8", result[0].ShareClassFigi);
        Assert.Equal("AAPL", result[0].Ticker);
        Assert.Equal("Apple Inc", result[0].Name);
        Assert.Equal("US", result[0].ExchCode);
        Assert.Equal("Common Stock", result[0].SecurityType);
        Assert.Equal("Common Stock", result[0].SecurityType2);
        Assert.Equal("Equity", result[0].MarketSector);
    }

    [Fact]
    public async Task MapByIsinAsync_WithValidIsin_ReturnsAllVenues()
    {
        // Arrange — a single job whose "data" array lists every venue for the ISIN. OpenFIGI does not
        // echo the ISIN; the client stamps the queried value back onto each result as a cross-reference.
        var httpHandler = new MockHttpMessageHandler(response: """
            [
              {
                "data": [
                  {
                    "figi": "BBG00B3T3HD3",
                    "shareClassFIGI": "BBG001S5W9D1",
                    "ticker": "CSPX",
                    "name": "iShares Core S&P 500 ETF",
                    "exchCode": "LN",
                    "securityType": "ETP",
                    "securityType2": "Mutual Fund",
                    "marketSector": "Equity"
                  },
                  {
                    "figi": "BBG00B3T3HF1",
                    "shareClassFIGI": "BBG001S5W9D1",
                    "ticker": "CSPX",
                    "name": "iShares Core S&P 500 ETF",
                    "exchCode": "SX",
                    "securityType": "ETP",
                    "securityType2": "Mutual Fund",
                    "marketSector": "Equity"
                  }
                ]
              }
            ]
            """);

        var client = CreateClient(httpHandler);

        // Act
        var result = await client.MapByIsinAsync("IE00B5BMR087", TestContext.Current.CancellationToken);

        // Assert — the queried ISIN is stamped onto every venue, and shareClassFigi is carried through.
        Assert.Equal(2, result.Count);
        Assert.All(result, x => Assert.Equal("IE00B5BMR087", x.Isin));
        Assert.All(result, x => Assert.Equal("BBG001S5W9D1", x.ShareClassFigi));
        Assert.Contains(result, x => x.ExchCode == "LN");
        Assert.Contains(result, x => x.ExchCode == "SX");
    }

    [Fact]
    public async Task MapByTickerAsync_WithEmptyTicker_ReturnsEmpty()
    {
        // Arrange
        var httpHandler = new MockHttpMessageHandler(response: "[]");
        var client = CreateClient(httpHandler);

        // Act
        var result = await client.MapByTickerAsync(string.Empty, ct: TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task MapByTickerAsync_WhenApiReturnsFailure_Throws()
    {
        // Arrange — transport failures must surface so callers can enter cooldown.
        var httpHandler = new MockHttpMessageHandler(
            statusCode: HttpStatusCode.TooManyRequests,
            response: "Rate limited");

        var client = CreateClient(httpHandler);

        // Act / Assert
        await Assert.ThrowsAsync<HttpRequestException>(
            () => client.MapByTickerAsync("AAPL", "US", TestContext.Current.CancellationToken));
    }

    private sealed class StubExternalServiceConfigService(ExternalServiceConfiguration config) : IExternalServiceConfigService
    {
        public ValueTask<ExternalServiceConfiguration> GetServiceAsync(string serviceName, CancellationToken ct = default) =>
            ValueTask.FromResult(config);

        public Task SaveServiceAsync(ExternalServiceConfiguration cfg, CancellationToken ct = default) =>
            Task.CompletedTask;
    }

    private sealed class MockHttpMessageHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _statusCode;
        private readonly string _response;

        public bool LastRequestHadApiKey { get; private set; }

        public MockHttpMessageHandler(HttpStatusCode statusCode = HttpStatusCode.OK, string response = "")
        {
            _statusCode = statusCode;
            _response = response;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.Content?.Headers.Contains("X-OPENFIGI-APIKEY") ?? false)
                LastRequestHadApiKey = true;

            return Task.FromResult(new HttpResponseMessage
            {
                StatusCode = _statusCode,
                Content = new StringContent(_response)
            });
        }
    }
}