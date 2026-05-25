using FinanceManager.Application.Services.Stocks;
using FinanceManager.Domain.Dtos;
using FinanceManager.Domain.Entities.Currencies;
using FinanceManager.Domain.Entities.Stocks;
using FinanceManager.Domain.Repositories;
using FinanceManager.Domain.Services;
using Moq;

namespace FinanceManager.UnitTests.Application.Services;

[Collection("Application")]
[Trait("Category", "Unit")]
public class StockMarketServiceTests : IDisposable
{
    private readonly Mock<IAlphaVantageClient> _apiClient = new();
    private readonly Mock<IStockPriceProvider> _stockPriceProvider = new();
    private readonly Mock<ICurrencyRepository> _currencyRepository = new();

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
    public async Task GetStockPrices_DelegatesToProvider()
    {
        // Arrange
        var start = new DateTime(2026, 2, 9);
        var end = new DateTime(2026, 2, 10);
        var expected = new List<StockPrice>
        {
            new() { Isin = "GB0002374006", PricePerUnit = 747.18m, Currency = new Currency(1, "USD", "$"), Date = end },
            new() { Isin = "GB0002374006", PricePerUnit = 747.02m, Currency = new Currency(1, "USD", "$"), Date = start }
        };
        _stockPriceProvider.Setup(p => p.GetPricesAsync("CSPX.LON", start, end, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);
        var service = CreateService();

        // Act
        var result = await service.GetStockPrices("CSPX.LON", start, end, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(expected, result);
        _stockPriceProvider.Verify(p => p.GetPricesAsync("CSPX.LON", start, end, It.IsAny<CancellationToken>()), Times.Once);
    }

    private StockMarketService CreateService()
    {
        var isinResolverMock = new Mock<IIsinResolver>();
        return new(
            _apiClient.Object,
            _stockPriceProvider.Object,
            _currencyRepository.Object,
            isinResolverMock.Object);
    }

    public void Dispose() { }
}