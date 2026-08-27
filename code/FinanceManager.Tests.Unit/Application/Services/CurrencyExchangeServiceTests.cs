using FinanceManager.Application.FinancialAccounts.Currencies.ExchangeRates;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Currencies.Repositories;
using FinanceManager.Domain.FinancialAccounts.Currencies.Services;
using FinanceManager.Domain.FinancialAccounts.Investments.Entities;
using FinanceManager.Domain.FinancialAccounts.Shared.Services;
using FinanceManager.Domain.Identity.Services;
using FinanceManager.Infrastructure.Features.FinancialAccounts.Currencies.Providers;
using FinanceManager.Tests.Unit.Shared.Time;
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
    private readonly ListLogger<FawazAhmedCurrencyApiClient> _logger = new();
    private readonly Mock<HttpMessageHandler> _httpMessageHandlerMock = new();
    private readonly Mock<IExchangeRateRepository> _exchangeRateRepositoryMock = new();
    private readonly FakeDateTimeProvider _dateTimeProvider = new(new DateTime(2024, 4, 1, 12, 0, 0, DateTimeKind.Utc));
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
        var date = new DateTime(2024, 3, 15);
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
    public async Task GetExchangeRateAsync_HttpRequestFails_ReturnsFailed()
    {
        // Arrange
        var fromCurrency = new Currency(1, "USD", "$");
        var toCurrency = new Currency(2, "EUR", "€");
        var date = new DateTime(2024, 3, 15);

        _httpMessageHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Network error"));

        var provider = CreateProvider();

        // Act
        var result = await provider.GetExchangeRateAsync(fromCurrency, toCurrency, date);

        // Assert
        Assert.Equal(CurrencyExchangeRateProviderStatus.Failed, result.Status);
    }

    [Fact]
    public async Task GetExchangeRateAsync_InvalidJson_ReturnsFailed()
    {
        // Arrange
        var fromCurrency = new Currency(1, "USD", "$");
        var toCurrency = new Currency(2, "EUR", "€");
        var date = new DateTime(2024, 3, 15);

        SetupHttpResponse(HttpStatusCode.OK, "invalid json {");
        var provider = CreateProvider();

        // Act
        var result = await provider.GetExchangeRateAsync(fromCurrency, toCurrency, date);

        // Assert
        Assert.Equal(CurrencyExchangeRateProviderStatus.Failed, result.Status);
    }

    [Fact]
    public async Task GetExchangeRateAsync_MissingRateInResponse_ReturnsNotFound()
    {
        // Arrange
        var fromCurrency = new Currency(1, "USD", "$");
        var toCurrency = new Currency(2, "EUR", "€");
        var date = new DateTime(2024, 3, 15);

        var jsonResponse = @"{""usd"": {""gbp"": 0.78}}"; // EUR missing
        SetupHttpResponse(HttpStatusCode.OK, jsonResponse);

        var provider = CreateProvider();

        // Act
        var result = await provider.GetExchangeRateAsync(fromCurrency, toCurrency, date);

        // Assert
        Assert.Equal(CurrencyExchangeRateProviderStatus.NotFound, result.Status);
    }

    [Fact]
    public async Task GetExchangeRateAsync_NotFoundResponse_ReturnsNotFound()
    {
        // Arrange
        var fromCurrency = new Currency(1, "USD", "$");
        var toCurrency = new Currency(2, "EUR", "€");
        var date = new DateTime(2024, 3, 15);

        SetupHttpResponse(HttpStatusCode.NotFound, "Not found");
        var provider = CreateProvider();

        // Act
        var result = await provider.GetExchangeRateAsync(fromCurrency, toCurrency, date);

        // Assert
        Assert.Equal(CurrencyExchangeRateProviderStatus.NotFound, result.Status);
    }

    [Fact]
    public async Task GetExchangeRateAsync_CurrentDateNotPublished_ReturnsRetryWindow()
    {
        var todayUtc = new DateTime(2024, 4, 1, 7, 12, 0, DateTimeKind.Utc);
        _dateTimeProvider.SetUtcNow(todayUtc);
        SetupHttpResponse(HttpStatusCode.NotFound, "Not published");

        var result = await CreateProvider().GetExchangeRateAsync(
            new Currency(1, "USD", "$"),
            new Currency(2, "EUR", "€"),
            todayUtc.Date);

        Assert.Equal(CurrencyExchangeRateProviderStatus.NotYetPublished, result.Status);
        Assert.Equal(todayUtc.Date.AddDays(1), result.RetryAtUtc);
        VerifyNoWarningOrErrorLogs();
    }

    [Fact]
    public async Task GetExchangeRateAsync_BeforeFirstAvailableDate_ReturnsOutOfRangeWithoutHttpOrWarning()
    {
        var result = await CreateProvider().GetExchangeRateAsync(
            new Currency(1, "USD", "$"),
            new Currency(2, "EUR", "€"),
            new DateTime(2024, 3, 1));

        Assert.Equal(CurrencyExchangeRateProviderStatus.OutOfRange, result.Status);
        _httpMessageHandlerMock.Protected().Verify(
            "SendAsync",
            Times.Never(),
            ItExpr.IsAny<HttpRequestMessage>(),
            ItExpr.IsAny<CancellationToken>());
        VerifyNoWarningOrErrorLogs();
    }

    [Fact]
    public async Task GetExchangeRateAsync_FirstAvailableDate_PerformsNormalRequest()
    {
        SetupHttpResponse(HttpStatusCode.OK, """{"usd":{"eur":0.92}}""");

        var result = await CreateProvider().GetExchangeRateAsync(
            new Currency(1, "USD", "$"),
            new Currency(2, "EUR", "€"),
            new DateTime(2024, 3, 2));

        Assert.Equal(new(CurrencyExchangeRateProviderStatus.Success, 0.92m), result);
        _httpMessageHandlerMock.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.IsAny<HttpRequestMessage>(),
            ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task GetExchangeRateAsync_MixedRange_OnlyFetchesSupportedDatesInOrder()
    {
        SetupHttpResponse(HttpStatusCode.OK, """{"usd":{"eur":0.92}}""");

        var result = await CreateProvider().GetExchangeRateAsync(
            new Currency(1, "USD", "$"),
            new Currency(2, "EUR", "€"),
            new DateTime(2024, 3, 1),
            new DateTime(2024, 3, 3));

        Assert.Equal(
            [
                CurrencyExchangeRateProviderStatus.OutOfRange,
                CurrencyExchangeRateProviderStatus.Success,
                CurrencyExchangeRateProviderStatus.Success,
            ],
            result.Select(x => x.Result.Status));
        Assert.Equal(
            [new DateTime(2024, 3, 1), new DateTime(2024, 3, 2), new DateTime(2024, 3, 3)],
            result.Select(x => x.Date));
        _httpMessageHandlerMock.Protected().Verify(
            "SendAsync",
            Times.Exactly(2),
            ItExpr.IsAny<HttpRequestMessage>(),
            ItExpr.IsAny<CancellationToken>());
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
        var date = new DateTime(2024, 3, 2); // Saturday
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
        var date = new DateTime(2024, 3, 15);

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
        var date = new DateTime(2024, 3, 15);
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
        var date = new DateTime(2024, 3, 15);
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
        var date = new DateTime(2024, 3, 15);

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
        var date = new DateTime(2024, 3, 15);

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
        var date = new DateTime(2024, 3, 15);

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
        var date = new DateTime(2024, 3, 15);

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
    public async Task GetExchangeRateAsync_OutOfRangeProvider_ContinuesToLaterProvider()
    {
        var fromCurrency = new Currency(1, "USD", "$");
        var toCurrency = new Currency(2, "EUR", "€");
        var date = new DateTime(2024, 3, 1);
        var laterProvider = new Mock<ICurrencyExchangeRateProvider>();
        laterProvider
            .Setup(x => x.GetExchangeRateAsync(fromCurrency, toCurrency, date))
            .ReturnsAsync(new CurrencyExchangeRateProviderResult(CurrencyExchangeRateProviderStatus.Success, 0.92m));

        var service = CreateService([CreateProvider(), laterProvider.Object]);

        var result = await service.GetExchangeRateAsync(fromCurrency, toCurrency, date);

        Assert.Equal(0.92m, result);
        laterProvider.Verify(x => x.GetExchangeRateAsync(fromCurrency, toCurrency, date), Times.Once);
        _exchangeRateRepositoryMock.Verify(
            x => x.Add("USD", "EUR", date, 0.92m, It.IsAny<CancellationToken>()),
            Times.Once);
        _httpMessageHandlerMock.Protected().Verify(
            "SendAsync",
            Times.Never(),
            ItExpr.IsAny<HttpRequestMessage>(),
            ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task GetExchangeRateAsync_OutOfRangeProvider_IsNotRetriedForUsdCross()
    {
        var provider = new Mock<ICurrencyExchangeRateProvider>();
        provider
            .Setup(x => x.GetExchangeRateAsync(
                It.IsAny<Currency>(),
                It.IsAny<Currency>(),
                It.IsAny<DateTime>()))
            .ReturnsAsync(new CurrencyExchangeRateProviderResult(CurrencyExchangeRateProviderStatus.OutOfRange));
        var service = CreateService([provider.Object]);

        var result = await service.GetExchangeRateAsync(
            new Currency(1, "GBP", "£"),
            new Currency(2, "PLN", "zł"),
            new DateTime(2024, 3, 1));

        Assert.Null(result);
        provider.Verify(
            x => x.GetExchangeRateAsync(It.IsAny<Currency>(), It.IsAny<Currency>(), It.IsAny<DateTime>()),
            Times.Once);
    }

    [Fact]
    public async Task GetExchangeRateResultAsync_CurrentDateNotPublished_ReturnsMessageWithoutUsdCross()
    {
        var todayUtc = _dateTimeProvider.UtcNow.Date;
        var provider = new Mock<ICurrencyExchangeRateProvider>();
        provider
            .Setup(x => x.GetExchangeRateAsync(It.IsAny<Currency>(), It.IsAny<Currency>(), todayUtc))
            .ReturnsAsync(new CurrencyExchangeRateProviderResult(
                CurrencyExchangeRateProviderStatus.NotYetPublished,
                RetryAtUtc: todayUtc.AddDays(1)));
        var service = CreateService([provider.Object]);

        var result = await service.GetExchangeRateResultAsync(
            new Currency(1, "GBP", "£"),
            new Currency(2, "PLN", "zł"),
            todayUtc);

        Assert.Equal(CurrencyExchangeRateStatus.NotYetPublished, result.Status);
        Assert.Equal(todayUtc.AddDays(1), result.RetryAtUtc);
        Assert.Equal(
            "The exchange rate for 2024-04-01 UTC has not been published yet. It is safe to retry after 2024-04-02T00:00:00Z.",
            result.Message);
        provider.Verify(
            x => x.GetExchangeRateAsync(It.IsAny<Currency>(), It.IsAny<Currency>(), It.IsAny<DateTime>()),
            Times.Once);
    }

    [Fact]
    public async Task GetExchangeRateAsync_FullyStoredRange_UsesBulkReadAndNoProviderCalls()
    {
        // Arrange
        var fromCurrency = new Currency(1, "USD", "$");
        var toCurrency = new Currency(2, "EUR", "€");
        var dateStart = new DateTime(2024, 3, 15);
        var dateEnd = new DateTime(2024, 3, 17);

        // Setup bulk read to return all rates
        IReadOnlyDictionary<(string From, string To, DateTime Date), decimal> storedRates =
            new Dictionary<(string, string, DateTime), decimal>
            {
                { ("USD", "EUR", new DateTime(2024, 3, 15, 0, 0, 0, DateTimeKind.Utc)), 0.92m },
                { ("USD", "EUR", new DateTime(2024, 3, 16, 0, 0, 0, DateTimeKind.Utc)), 0.91m },
                { ("USD", "EUR", new DateTime(2024, 3, 17, 0, 0, 0, DateTimeKind.Utc)), 0.93m }
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
        var dateStart = new DateTime(2024, 3, 15);
        var dateEnd = new DateTime(2024, 3, 17);

        // Setup bulk read to return only first and last rates
        IReadOnlyDictionary<(string From, string To, DateTime Date), decimal> storedRates =
            new Dictionary<(string, string, DateTime), decimal>
            {
                { ("USD", "EUR", new DateTime(2024, 3, 15, 0, 0, 0, DateTimeKind.Utc)), 0.92m },
                { ("USD", "EUR", new DateTime(2024, 3, 17, 0, 0, 0, DateTimeKind.Utc)), 0.93m }
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

    [Fact]
    public async Task GetExchangeRateAsync_RangeWithManyMissingDates_CapsProviderCallsAndCarriesRatesForward()
    {
        // Arrange: a 100-day range with only the first day stored, so 99 dates are missing —
        // more than the per-call provider resolution cap of 60.
        var fromCurrency = new Currency(1, "USD", "$");
        var toCurrency = new Currency(2, "EUR", "€");
        var dateStart = new DateTime(2024, 3, 2);
        var dateEnd = dateStart.AddDays(99);

        IReadOnlyDictionary<(string From, string To, DateTime Date), decimal> storedRates =
            new Dictionary<(string, string, DateTime), decimal>
            {
                { ("USD", "EUR", new DateTime(2024, 3, 2, 0, 0, 0, DateTimeKind.Utc)), 0.92m }
            };

        _exchangeRateRepositoryMock
            .Setup(x => x.GetRange("USD", "EUR", It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(storedRates);
        _exchangeRateRepositoryMock
            .Setup(x => x.Get(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((decimal?)null);

        var jsonResponse = @"{""usd"": {""eur"": 0.915}}";
        SetupHttpResponse(HttpStatusCode.OK, jsonResponse);

        var service = CreateService();

        // Act
        var result = await service.GetExchangeRateAsync(fromCurrency, toCurrency, dateStart, dateEnd);

        // Assert: every date has a value — the capped tail is forward-filled, not dropped.
        Assert.Equal(100, result.Count);
        Assert.All(result, x => Assert.NotNull(x.Value));
        Assert.Equal(0.915m, result[^1].Value);

        // Only the first 60 missing dates may reach the external provider.
        _httpMessageHandlerMock.Protected().Verify(
            "SendAsync",
            Times.Exactly(60),
            ItExpr.IsAny<HttpRequestMessage>(),
            ItExpr.IsAny<CancellationToken>());
    }

    private CurrencyExchangeService CreateService()
    {
        ICurrencyExchangeRateProvider[] providers = [CreateProvider()];
        return new CurrencyExchangeService(_exchangeRateRepositoryMock.Object, providers);
    }

    private CurrencyExchangeService CreateService(ICurrencyExchangeRateProvider[] providers) =>
        new(_exchangeRateRepositoryMock.Object, providers);

    private FawazAhmedCurrencyApiClient CreateProvider() => new(_httpClient, _logger, _dateTimeProvider);

    private void VerifyNoWarningOrErrorLogs() =>
        Assert.DoesNotContain(_logger.Levels, level => level >= LogLevel.Warning);

    private sealed class ListLogger<T> : ILogger<T>
    {
        public List<LogLevel> Levels { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            Levels.Add(logLevel);
    }
    public void Dispose() => _httpClient?.Dispose();
    private void SetupHttpResponse(HttpStatusCode statusCode, string content)
    {
        _httpMessageHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            // A fresh response per request: the content stream is single-use, so a shared
            // instance would fail every request after the first.
            .ReturnsAsync(() => new HttpResponseMessage
            {
                StatusCode = statusCode,
                Content = new StringContent(content, Encoding.UTF8, "application/json")
            });
    }
}