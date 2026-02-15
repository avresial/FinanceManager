using FinanceManager.Application.Services.Stocks;
using FinanceManager.Domain.Dtos;
using FinanceManager.Domain.Entities.Currencies;
using FinanceManager.Domain.Entities.Stocks;
using FinanceManager.Domain.Repositories;
using Moq;

namespace FinanceManager.UnitTests.Application.Services;

[Collection("Application")]
[Trait("Category", "Unit")]
public class StockMarketServiceTests : IDisposable
{
    private readonly Mock<IAlphaVantageClient> _apiClient = new();
    private readonly Mock<IStockPriceRepository> _stockPriceRepository = new();
    private readonly Mock<ICurrencyRepository> _currencyRepository = new();
    private readonly Mock<IStockDetailsRepository> _stockDetailsRepository = new();

    public StockMarketServiceTests()
    {
    }

    [Fact]
    public async Task SearchTicker_MapsAllFields()
    {
        // Arrange
        var matches = new List<TickerSearchMatch>
        {
            new()
            {
                Symbol = "CSPX.LON",
                Name = "iShares Core S&P 500 UCITS ETF USD (Acc)",
                Type = "ETF",
                Region = "United Kingdom",
                MarketOpen = "08:00",
                MarketClose = "16:30",
                Timezone = "UTC+01",
                Currency = "USD",
                MatchScore = 0.8000m
            }
        };
        _apiClient.Setup(client => client.SearchTicker("CSPX", It.IsAny<CancellationToken>()))
            .ReturnsAsync(matches);
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

        var apiPrices = new List<StockPrice>
        {
            new() { Ticker = "CSPX.LON", PricePerUnit = 747.18m, Currency = new Currency(1, "USD", "$"), Date = end },
            new() { Ticker = "CSPX.LON", PricePerUnit = 747.02m, Currency = new Currency(1, "USD", "$"), Date = start }
        };
        _apiClient.Setup(client => client.GetDailySeries("CSPX.LON", start, end, It.IsAny<Currency>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(apiPrices);
        var service = CreateService();

        // Act
        var result = await service.GetStockPrices("CSPX.LON", start, end, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(2, result.Count);
        Assert.True(result[0].Date >= result[1].Date);
        _stockPriceRepository.Verify(repo => repo.Add(It.IsAny<IEnumerable<StockPrice>>()), Times.Once);
    }

    private StockMarketService CreateService() => new(
        _apiClient.Object,
        _stockPriceRepository.Object,
        _currencyRepository.Object,
        _stockDetailsRepository.Object);

    public void Dispose() { }
}