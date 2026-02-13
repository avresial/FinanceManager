using FinanceManager.Application.Options;
using FinanceManager.Application.Services.Stocks;
using FinanceManager.Domain.Entities.Currencies;
using FinanceManager.Domain.Entities.Stocks;
using FinanceManager.Domain.Repositories;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using System.Net;
using System.Text;

namespace FinanceManager.UnitTests.Application.Services;

[Collection("Application")]
[Trait("Category", "Unit")]
public class StockMarketServiceTests : IDisposable
{
    private readonly ILogger<StockMarketService> _logger = LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger<StockMarketService>();
    private readonly Mock<HttpMessageHandler> _httpMessageHandlerMock = new();
    private readonly HttpClient _httpClient;
    private readonly Mock<IStockPriceRepository> _stockPriceRepository = new();
    private readonly Mock<ICurrencyRepository> _currencyRepository = new();
    private readonly Mock<IStockDetailsRepository> _stockDetailsRepository = new();
    private readonly IOptions<StockApiOptions> _options;

    public StockMarketServiceTests()
    {
        _httpClient = new HttpClient(_httpMessageHandlerMock.Object);
        _options = Options.Create(new StockApiOptions
        {
            BaseUrl = "https://www.alphavantage.co/query",
            ApiKey = "test-key",
            OutputSize = "compact"
        });
    }

    [Fact]
    public async Task SearchTicker_MapsAllFields()
    {
        // Arrange
        var jsonResponse = """
{
    "bestMatches": [
        {
            "1. symbol": "CSPX.LON",
            "2. name": "iShares Core S&P 500 UCITS ETF USD (Acc)",
            "3. type": "ETF",
            "4. region": "United Kingdom",
            "5. marketOpen": "08:00",
            "6. marketClose": "16:30",
            "7. timezone": "UTC+01",
            "8. currency": "USD",
            "9. matchScore": "0.8000"
        }
    ]
}
""";

        SetupHttpResponse("function=SYMBOL_SEARCH", jsonResponse);
        var service = CreateService();

        // Act
        var result = await service.SearchTicker("CSPX", TestContext.Current.CancellationToken);

        // Assert
        Assert.Single(result);
        var match = result[0];
        Assert.Equal("CSPX.LON", match.Symbol);
        Assert.Equal("iShares Core S&P 500 UCITS ETF USD (Acc)", match.Name);
        Assert.Equal("ETF", match.Type);
        Assert.Equal("United Kingdom", match.Region);
        Assert.Equal("08:00", match.MarketOpen);
        Assert.Equal("16:30", match.MarketClose);
        Assert.Equal("UTC+01", match.Timezone);
        Assert.Equal("USD", match.Currency);
        Assert.Equal(0.8000m, match.MatchScore);
    }

    [Fact]
    public async Task GetDailyStock_FetchesAndAddsWhenMissing()
    {
        // Arrange
        var start = new DateTime(2026, 2, 9);
        var end = new DateTime(2026, 2, 10);
        _stockPriceRepository.Setup(repo => repo.GetRange("CSPX.LON", start, end))
            .ReturnsAsync(Array.Empty<StockPrice>());
        _stockDetailsRepository.Setup(repo => repo.Get("CSPX.LON", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StockDetails
            {
                Ticker = "CSPX.LON",
                Name = "Test",
                Type = "ETF",
                Region = "UK",
                Currency = new Currency(1, "USD", "$")
            });

        var jsonResponse = """
{
    "Time Series (Daily)": {
        "2026-02-10": { "4. close": "747.1800" },
        "2026-02-09": { "4. close": "747.0200" }
    }
}
""";

        SetupHttpResponse("function=TIME_SERIES_DAILY", jsonResponse);
        var service = CreateService();

        // Act
        var result = await service.GetDailyStock("CSPX.LON", start, end, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(2, result.Count);
        Assert.True(result[0].Date >= result[1].Date);
        _stockPriceRepository.Verify(repo => repo.Add(It.IsAny<IEnumerable<StockPrice>>()), Times.Once);
    }

    private void SetupHttpResponse(string queryFragment, string jsonResponse)
    {
        _httpMessageHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.ToString().Contains(queryFragment)),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(jsonResponse, Encoding.UTF8, "application/json")
            });
    }

    private StockMarketService CreateService() => new(
        _httpClient,
        _logger,
        _options,
        _stockPriceRepository.Object,
        _currencyRepository.Object,
        _stockDetailsRepository.Object);

    public void Dispose() => _httpClient.Dispose();
}