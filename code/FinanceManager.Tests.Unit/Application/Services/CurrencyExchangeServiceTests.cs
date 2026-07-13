using FinanceManager.Application.FinancialAccounts.Currencies.ExchangeRates;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Currencies.Repositories;
using FinanceManager.Domain.FinancialAccounts.Currencies.Services;
using FinanceManager.Domain.FinancialAccounts.Investments.Entities;
using FinanceManager.Domain.FinancialAccounts.Shared.Services;
using FinanceManager.Domain.Identity.Services;
using FinanceManager.Infrastructure.Services.Currencies;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using System.Net;
using System.Text;

namespace FinanceManager.Tests.Unit.Application.Services;

[Collection("Application")]
[Trait("Category", "Unit")]
public class CurrencyExchangeServiceTests : IDisposable
{
    private readonly ILogger<FawazAhmedCurrencyApiClient> _loggerMock = LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger<FawazAhmedCurrencyApiClient>();
    private readonly Mock<HttpMessageHandler> _httpMessageHandlerMock = new();
    private readonly Mock<IExchangeRateRepository> _exchangeRateRepositoryMock = new();
    private readonly HttpClient _httpClient;

    public CurrencyExchangeServiceTests()
    {
        _httpClient = new HttpClient(_httpMessageHandlerMock.Object);
    }

    [Fact]
    public async Task GetExchangeRateAsync_ValidRequest_ReturnsRate()
    {
        // Arrange
        var fromCurrency = new Currency(1, "USD", "$");
        var toCurrency = new Currency(2, "EUR", "€");
        var date = new DateTime(2024, 1, 15);
        var expectedRate = 0.92m;

        var jsonResponse = $@"{{""usd"": {{""eur"": {expectedRate}}}}}";
        SetupHttpResponse(HttpStatusCode.OK, jsonResponse);

        var service = CreateService();

        // Act
        var result = await service.GetExchangeRateAsync(fromCurrency, toCurrency, date);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(expectedRate, result.Value);
    }

    [Fact]
    public async Task GetExchangeRateAsync_HttpRequestFails_ReturnsNull()
    {
        // Arrange
        var fromCurrency = new Currency(1, "USD", "$");
        var toCurrency = new Currency(2, "EUR", "€");
        var date = new DateTime(2024, 1, 15);

        _httpMessageHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Network error"));

        var service = CreateService();

        // Act
        var result = await service.GetExchangeRateAsync(fromCurrency, toCurrency, date);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetExchangeRateAsync_InvalidJson_ReturnsNull()
    {
        // Arrange
        var fromCurrency = new Currency(1, "USD", "$");
        var toCurrency = new Currency(2, "EUR", "€");
        var date = new DateTime(2024, 1, 15);

        SetupHttpResponse(HttpStatusCode.OK, "invalid json {");
        var service = CreateService();

        // Act
        var result = await service.GetExchangeRateAsync(fromCurrency, toCurrency, date);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetExchangeRateAsync_MissingRateInResponse_ReturnsNull()
    {
        // Arrange
        var fromCurrency = new Currency(1, "USD", "$");
        var toCurrency = new Currency(2, "EUR", "€");
        var date = new DateTime(2024, 1, 15);

        var jsonResponse = @"{""usd"": {""gbp"": 0.78}}"; // EUR missing
        SetupHttpResponse(HttpStatusCode.OK, jsonResponse);

        var service = CreateService();

        // Act
        var result = await service.GetExchangeRateAsync(fromCurrency, toCurrency, date);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetExchangeRateAsync_NotFoundResponse_ReturnsNull()
    {
        // Arrange
        var fromCurrency = new Currency(1, "USD", "$");
        var toCurrency = new Currency(2, "EUR", "€");
        var date = new DateTime(2024, 1, 15);

        SetupHttpResponse(HttpStatusCode.NotFound, "Not found");
        var service = CreateService();

        // Act
        var result = await service.GetExchangeRateAsync(fromCurrency, toCurrency, date);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetExchangeRateAsync_CorrectUrlFormat_CallsExpectedEndpoint()
    {
        // Arrange
        var fromCurrency = new Currency(1, "USD", "$");
        var toCurrency = new Currency(2, "GBP", "£");
        var date = new DateTime(2024, 3, 20);

        var jsonResponse = @"{""usd"": {""gbp"": 0.78}}";
        SetupHttpResponse(HttpStatusCode.OK, jsonResponse);

        var service = CreateService();

        // Act
        await service.GetExchangeRateAsync(fromCurrency, toCurrency, date);

        // Assert
        _httpMessageHandlerMock.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.Is<HttpRequestMessage>(req =>
                req.RequestUri!.ToString() == "https://cdn.jsdelivr.net/npm/@fawazahmed0/currency-api@2024-03-20/v1/currencies/usd.json"),
            ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task GetExchangeRateAsync_WeekendDate_HandlesCorrectly()
    {
        // Arrange
        var fromCurrency = new Currency(1, "USD", "$");
        var toCurrency = new Currency(2, "EUR", "€");
        var date = new DateTime(2024, 1, 6); // Saturday
        var expectedRate = 0.92m;

        var jsonResponse = $@"{{""usd"": {{""eur"": {expectedRate}}}}}";
        SetupHttpResponse(HttpStatusCode.OK, jsonResponse);

        var service = CreateService();

        // Act
        var result = await service.GetExchangeRateAsync(fromCurrency, toCurrency, date);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(expectedRate, result.Value);
    }

    [Fact]
    public async Task GetExchangeRateAsync_EmptyResponse_ReturnsNull()
    {
        // Arrange
        var fromCurrency = new Currency(1, "USD", "$");
        var toCurrency = new Currency(2, "EUR", "€");
        var date = new DateTime(2024, 1, 15);

        SetupHttpResponse(HttpStatusCode.OK, "{}");
        var service = CreateService();

        // Act
        var result = await service.GetExchangeRateAsync(fromCurrency, toCurrency, date);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetExchangeRateAsync_VeryLargeRate_HandlesCorrectly()
    {
        // Arrange
        var fromCurrency = new Currency(1, "USD", "$");
        var toCurrency = new Currency(2, "JPY", "¥");
        var date = new DateTime(2024, 1, 15);
        var expectedRate = 149.85m;

        var jsonResponse = $@"{{""usd"": {{""jpy"": {expectedRate}}}}}";
        SetupHttpResponse(HttpStatusCode.OK, jsonResponse);

        var service = CreateService();

        // Act
        var result = await service.GetExchangeRateAsync(fromCurrency, toCurrency, date);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(expectedRate, result.Value);
    }

    [Fact]
    public async Task GetExchangeRateAsync_VerySmallRate_HandlesCorrectly()
    {
        // Arrange
        var fromCurrency = new Currency(1, "JPY", "¥");
        var toCurrency = new Currency(2, "USD", "$");
        var date = new DateTime(2024, 1, 15);
        var expectedRate = 0.006678m;

        var jsonResponse = $@"{{""jpy"": {{""usd"": {expectedRate}}}}}";
        SetupHttpResponse(HttpStatusCode.OK, jsonResponse);

        var service = CreateService();

        // Act
        var result = await service.GetExchangeRateAsync(fromCurrency, toCurrency, date);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(expectedRate, result.Value);
    }

    [Fact]
    public async Task GetExchangeRateAsync_RateStoredInDatabase_DoesNotCallExternalProvider()
    {
        // Arrange
        var fromCurrency = new Currency(1, "USD", "$");
        var toCurrency = new Currency(2, "EUR", "€");
        var date = new DateTime(2024, 1, 15);

        _exchangeRateRepositoryMock
            .Setup(x => x.Get("USD", "EUR", date, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0.92m);

        var service = CreateService();

        // Act
        var result = await service.GetExchangeRateAsync(fromCurrency, toCurrency, date);

        // Assert
        Assert.Equal(0.92m, result);
        _httpMessageHandlerMock.Protected().Verify(
            "SendAsync",
            Times.Never(),
            ItExpr.IsAny<HttpRequestMessage>(),
            ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task GetExchangeRateAsync_InverseRateStoredInDatabase_ReturnsInvertedRateWithoutExternalCall()
    {
        // Arrange
        var fromCurrency = new Currency(1, "USD", "$");
        var toCurrency = new Currency(2, "EUR", "€");
        var date = new DateTime(2024, 1, 15);

        _exchangeRateRepositoryMock
            .Setup(x => x.Get("EUR", "USD", date, It.IsAny<CancellationToken>()))
            .ReturnsAsync(2m);

        var service = CreateService();

        // Act
        var result = await service.GetExchangeRateAsync(fromCurrency, toCurrency, date);

        // Assert
        Assert.Equal(0.5m, result);
        _httpMessageHandlerMock.Protected().Verify(
            "SendAsync",
            Times.Never(),
            ItExpr.IsAny<HttpRequestMessage>(),
            ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task GetExchangeRateAsync_ExternalProviderHit_PersistsRateToDatabase()
    {
        // Arrange
        var fromCurrency = new Currency(1, "USD", "$");
        var toCurrency = new Currency(2, "EUR", "€");
        var date = new DateTime(2024, 1, 15);

        var jsonResponse = @"{""usd"": {""eur"": 0.92}}";
        SetupHttpResponse(HttpStatusCode.OK, jsonResponse);

        var service = CreateService();

        // Act
        var result = await service.GetExchangeRateAsync(fromCurrency, toCurrency, date);

        // Assert
        Assert.Equal(0.92m, result);
        _exchangeRateRepositoryMock.Verify(x => x.Add("USD", "EUR", date, 0.92m, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetExchangeRateAsync_DirectPairUnavailable_FallsBackToUsdCrossRate()
    {
        // Arrange
        var fromCurrency = new Currency(1, "GBP", "£");
        var toCurrency = new Currency(2, "PLN", "zł");
        var date = new DateTime(2024, 1, 15);

        _exchangeRateRepositoryMock
            .Setup(x => x.Get("GBP", "USD", date, It.IsAny<CancellationToken>()))
            .ReturnsAsync(1.25m);
        _exchangeRateRepositoryMock
            .Setup(x => x.Get("USD", "PLN", date, It.IsAny<CancellationToken>()))
            .ReturnsAsync(4m);

        // The direct GBP→PLN lookup misses everywhere, including the external provider.
        SetupHttpResponse(HttpStatusCode.NotFound, "Not found");

        var service = CreateService();

        // Act
        var result = await service.GetExchangeRateAsync(fromCurrency, toCurrency, date);

        // Assert
        Assert.Equal(5m, result);
    }

    [Fact]
    public async Task GetExchangeRateAsync_FullyStoredRange_UsesBulkReadAndNoProviderCalls()
    {
        // Arrange
        var fromCurrency = new Currency(1, "USD", "$");
        var toCurrency = new Currency(2, "EUR", "€");
        var dateStart = new DateTime(2024, 1, 15);
        var dateEnd = new DateTime(2024, 1, 17);

        // Setup bulk read to return all rates
        IReadOnlyDictionary<(string From, string To, DateTime Date), decimal> storedRates =
            new Dictionary<(string, string, DateTime), decimal>
            {
                { ("USD", "EUR", new DateTime(2024, 1, 15, 0, 0, 0, DateTimeKind.Utc)), 0.92m },
                { ("USD", "EUR", new DateTime(2024, 1, 16, 0, 0, 0, DateTimeKind.Utc)), 0.91m },
                { ("USD", "EUR", new DateTime(2024, 1, 17, 0, 0, 0, DateTimeKind.Utc)), 0.93m }
            };

        _exchangeRateRepositoryMock
            .Setup(x => x.GetRange("USD", "EUR", It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(storedRates);

        var service = CreateService();

        // Act
        var result = await service.GetExchangeRateAsync(fromCurrency, toCurrency, dateStart, dateEnd);

        // Assert
        Assert.Equal(3, result.Count);
        Assert.Equal(0.92m, result[0].Value);
        Assert.Equal(0.91m, result[1].Value);
        Assert.Equal(0.93m, result[2].Value);

        // Verify bulk read was called
        _exchangeRateRepositoryMock.Verify(
            x => x.GetRange("USD", "EUR", It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()),
            Times.Once);

        // Verify no provider calls were made (HTTP handler not invoked)
        _httpMessageHandlerMock.Protected().Verify(
            "SendAsync",
            Times.Never(),
            ItExpr.IsAny<HttpRequestMessage>(),
            ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task GetExchangeRateAsync_PartiallyStoredRange_UsesBulkReadThenPointResolutionForMissing()
    {
        // Arrange
        var fromCurrency = new Currency(1, "USD", "$");
        var toCurrency = new Currency(2, "EUR", "€");
        var dateStart = new DateTime(2024, 1, 15);
        var dateEnd = new DateTime(2024, 1, 17);

        // Setup bulk read to return only first and last rates
        IReadOnlyDictionary<(string From, string To, DateTime Date), decimal> storedRates =
            new Dictionary<(string, string, DateTime), decimal>
            {
                { ("USD", "EUR", new DateTime(2024, 1, 15, 0, 0, 0, DateTimeKind.Utc)), 0.92m },
                { ("USD", "EUR", new DateTime(2024, 1, 17, 0, 0, 0, DateTimeKind.Utc)), 0.93m }
            };

        _exchangeRateRepositoryMock
            .Setup(x => x.GetRange("USD", "EUR", It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(storedRates);

        // Setup point Get to return null for direct, forcing provider call
        _exchangeRateRepositoryMock
            .Setup(x => x.Get(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((decimal?)null);

        var jsonResponse = @"{""usd"": {""eur"": 0.915}}";
        SetupHttpResponse(HttpStatusCode.OK, jsonResponse);

        var service = CreateService();

        // Act
        var result = await service.GetExchangeRateAsync(fromCurrency, toCurrency, dateStart, dateEnd);

        // Assert
        Assert.Equal(3, result.Count);
        Assert.Equal(0.92m, result[0].Value);
        Assert.Equal(0.915m, result[1].Value);
        Assert.Equal(0.93m, result[2].Value);

        // Verify bulk read was called
        _exchangeRateRepositoryMock.Verify(
            x => x.GetRange("USD", "EUR", It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()),
            Times.Once);

        // Verify point Get was called for missing date resolution
        _exchangeRateRepositoryMock.Verify(
            x => x.Get(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);

        // Verify provider was called for missing date
        _httpMessageHandlerMock.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.IsAny<HttpRequestMessage>(),
            ItExpr.IsAny<CancellationToken>());
    }

    private CurrencyExchangeService CreateService()
    {
        ICurrencyExchangeRateProvider[] providers = [new FawazAhmedCurrencyApiClient(_httpClient, _loggerMock)];
        return new CurrencyExchangeService(_exchangeRateRepositoryMock.Object, providers);
    }
    public void Dispose() => _httpClient?.Dispose();
    private void SetupHttpResponse(HttpStatusCode statusCode, string content)
    {
        _httpMessageHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = statusCode,
                Content = new StringContent(content, Encoding.UTF8, "application/json")
            });
    }
}