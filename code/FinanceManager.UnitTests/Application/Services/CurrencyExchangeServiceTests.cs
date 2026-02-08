using FinanceManager.Application.Services;
using FinanceManager.Domain.Entities.Currencies;
using FinanceManager.Domain.Entities.Stocks;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using System.Net;
using System.Text;

namespace FinanceManager.UnitTests.Application.Services;

[Collection("Application")]
[Trait("Category", "Unit")]
public class CurrencyExchangeServiceTests : IDisposable
{
    private readonly ILogger<CurrencyExchangeService> _loggerMock = LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger<CurrencyExchangeService>();
    private readonly Mock<HttpMessageHandler> _httpMessageHandlerMock = new();
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

    [Theory]
    [InlineData("USD", "EUR", 100.50, 0.92, 92.46)]
    [InlineData("EUR", "USD", 50.25, 1.087, 54.62)]
    [InlineData("GBP", "JPY", 1000, 180.5, 180500)]
    public async Task GetPricePerUnit_WithExchangeRate_ReturnsConvertedPrice(
        string fromCurrencyCode, string toCurrencyCode, decimal pricePerUnit, decimal exchangeRate, decimal expected)
    {
        // Arrange
        var fromCurrency = new Currency(1, fromCurrencyCode, "$");
        var toCurrency = new Currency(2, toCurrencyCode, "€");
        var date = new DateTime(2024, 1, 15);

        var stockPrice = new StockPrice { Ticker = "TEST", PricePerUnit = pricePerUnit, Currency = fromCurrency };
        var jsonResponse = $@"{{""{fromCurrencyCode.ToLower()}"": {{""{toCurrencyCode.ToLower()}"": {exchangeRate}}}}}";
        SetupHttpResponse(HttpStatusCode.OK, jsonResponse);

        var service = CreateService();

        // Act
        var result = await service.GetPricePerUnit(stockPrice, toCurrency, date);

        // Assert
        Assert.Equal(expected, result, 2);
    }

    [Fact]
    public async Task GetPricePerUnit_SameCurrency_ReturnsPriceUnchanged()
    {
        // Arrange
        var currency = new Currency(1, "USD", "$");
        var stockPrice = new StockPrice { Ticker = "AAPL", PricePerUnit = 150.75m, Currency = currency };
        var date = new DateTime(2024, 1, 15);

        var service = CreateService();

        // Act
        var result = await service.GetPricePerUnit(stockPrice, currency, date);

        // Assert
        Assert.Equal(150.75m, result);
    }

    [Fact]
    public async Task GetPricePerUnit_NullStockPrice_ReturnsOne()
    {
        // Arrange
        var currency = new Currency(1, "USD", "$");
        var date = new DateTime(2024, 1, 15);

        var service = CreateService();

        // Act
        var result = await service.GetPricePerUnit(null!, currency, date);

        // Assert
        Assert.Equal(1m, result);
    }

    [Fact]
    public async Task GetPricePerUnit_ExchangeRateFails_ReturnsOne()
    {
        // Arrange
        var fromCurrency = new Currency(1, "USD", "$");
        var toCurrency = new Currency(2, "EUR", "€");
        var stockPrice = new StockPrice { Ticker = "MSFT", PricePerUnit = 100m, Currency = fromCurrency };
        var date = new DateTime(2024, 1, 15);

        _httpMessageHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Network error"));

        var service = CreateService();

        // Act
        var result = await service.GetPricePerUnit(stockPrice, toCurrency, date);

        // Assert
        Assert.Equal(1m, result);
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
    public async Task GetPricePerUnit_ZeroExchangeRate_HandlesCorrectly()
    {
        // Arrange
        var fromCurrency = new Currency(1, "USD", "$");
        var toCurrency = new Currency(2, "EUR", "€");
        var stockPrice = new StockPrice { Ticker = "GOOGL", PricePerUnit = 100m, Currency = fromCurrency };
        var date = new DateTime(2024, 1, 15);

        var jsonResponse = @"{""usd"": {""eur"": 0}}";
        SetupHttpResponse(HttpStatusCode.OK, jsonResponse);

        var service = CreateService();

        // Act
        var result = await service.GetPricePerUnit(stockPrice, toCurrency, date);

        // Assert
        Assert.Equal(0m, result);
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

    private CurrencyExchangeService CreateService() => new CurrencyExchangeService(_httpClient, _loggerMock);
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