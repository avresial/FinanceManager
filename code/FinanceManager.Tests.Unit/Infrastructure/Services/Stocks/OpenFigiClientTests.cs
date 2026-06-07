using FinanceManager.Application.Services.ExternalServices;
using FinanceManager.Domain.Entities.ExternalServices;
using FinanceManager.Infrastructure.Services.Stocks;
using Microsoft.Extensions.Logging;
using System.Net;

namespace FinanceManager.Tests.Unit.Infrastructure.Services.Stocks;

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
    public async Task ResolveAsync_WithValidTicker_ReturnsIsin()
    {
        // Arrange
        var httpHandler = new MockHttpMessageHandler(response: """
            [
              {
                "figi": "BBG000B9XRY4",
                "ticker": "AAPL",
                "isin": "US0378331005",
                "compositeFigi": "BBG000HKWL63"
              }
            ]
            """);

        var client = CreateClient(httpHandler);

        // Act
        var result = await client.ResolveAsync("AAPL", "US", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("US0378331005", result);
    }

    [Fact]
    public async Task ResolveAsync_WithInvalidTicker_ReturnsNull()
    {
        // Arrange
        var httpHandler = new MockHttpMessageHandler(response: "[]");
        var client = CreateClient(httpHandler);

        // Act
        var result = await client.ResolveAsync("INVALID_TICKER", ct: TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task ResolveAsync_WithEmptyTicker_ReturnsNull()
    {
        // Arrange
        var httpHandler = new MockHttpMessageHandler(response: "[]");
        var client = CreateClient(httpHandler);

        // Act
        var result = await client.ResolveAsync(string.Empty, ct: TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task ResolveAsync_WithNullTicker_ReturnsNull()
    {
        // Arrange
        var httpHandler = new MockHttpMessageHandler(response: "[]");
        var client = CreateClient(httpHandler);

        // Act
        var result = await client.ResolveAsync(null!, ct: TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task ResolveAsync_WhenApiReturnsNoIsin_ReturnsNull()
    {
        // Arrange
        var httpHandler = new MockHttpMessageHandler(response: """
            [
              {
                "figi": "BBG000B9XRY4",
                "ticker": "AAPL",
                "compositeFigi": "BBG000HKWL63"
              }
            ]
            """);

        var client = CreateClient(httpHandler);

        // Act
        var result = await client.ResolveAsync("AAPL", ct: TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task ResolveAsync_WhenApiReturnsFailure_ReturnsNull()
    {
        // Arrange
        var httpHandler = new MockHttpMessageHandler(
            statusCode: HttpStatusCode.BadRequest,
            response: "Bad Request");

        var client = CreateClient(httpHandler);

        // Act
        var result = await client.ResolveAsync("AAPL", ct: TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task ResolveAsync_WithApiKey_IncludesKeyInRequest()
    {
        // Arrange
        var httpHandler = new MockHttpMessageHandler(response: """
            [
              {
                "figi": "BBG000B9XRY4",
                "ticker": "AAPL",
                "isin": "US0378331005",
                "compositeFigi": "BBG000HKWL63"
              }
            ]
            """);

        var client = CreateClient(httpHandler, apiKey: "test-api-key");

        // Act
        var result = await client.ResolveAsync("AAPL", ct: TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("US0378331005", result);
        Assert.True(httpHandler.LastRequestHadApiKey);
    }

    [Fact]
    public async Task ResolveAsync_WithRegion_PassesRegionToApi()
    {
        // Arrange
        var httpHandler = new MockHttpMessageHandler(response: """
            [
              {
                "figi": "BBG000B9XRY4",
                "ticker": "AAPL",
                "isin": "US0378331005",
                "compositeFigi": "BBG000HKWL63"
              }
            ]
            """);

        var client = CreateClient(httpHandler);

        // Act
        var result = await client.ResolveAsync("AAPL", "US", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("US0378331005", result);
    }

    private sealed class StubExternalServiceConfigService(ExternalServiceConfiguration config) : IExternalServiceConfigService
    {
        public ValueTask<ExternalServiceConfiguration> GetServiceAsync(string serviceName, CancellationToken ct = default) =>
            ValueTask.FromResult(config);

        public ValueTask<IReadOnlyList<ExternalServiceConfiguration>> GetAllServicesAsync(CancellationToken ct = default) =>
            ValueTask.FromResult<IReadOnlyList<ExternalServiceConfiguration>>([config]);

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