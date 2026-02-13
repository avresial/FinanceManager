using FinanceManager.Api.Controllers;
using FinanceManager.Domain.Dtos;
using FinanceManager.Domain.Entities.Currencies;
using FinanceManager.Domain.Entities.Stocks;
using FinanceManager.Domain.Repositories;
using FinanceManager.Domain.Services;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace FinanceManager.UnitTests.Api.Controllers;

[Collection("Api")]
[Trait("Category", "Unit")]
public class StockPriceControllerTests
{
    private readonly Mock<IStockPriceRepository> _stockPriceRepository = new();
    private readonly Mock<ICurrencyExchangeService> _currencyExchangeService = new();
    private readonly Mock<ICurrencyRepository> _currencyRepository = new();
    private readonly Mock<IStockMarketService> _stockMarketService = new();
    private readonly Mock<IStockDetailsRepository> _stockDetailsRepository = new();
    private readonly StockPriceController _controller;

    public StockPriceControllerTests()
    {
        _controller = new StockPriceController(
            _stockPriceRepository.Object,
            _currencyExchangeService.Object,
            _currencyRepository.Object,
            _stockMarketService.Object,
            _stockDetailsRepository.Object);
    }

    [Fact]
    public async Task SearchTicker_ReturnsNotFound_WhenNoMatches()
    {
        // Arrange
        _stockMarketService.Setup(service => service.SearchTicker("ABC", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<TickerSearchMatch>());

        // Act
        var result = await _controller.SearchTicker("ABC", TestContext.Current.CancellationToken);

        // Assert
        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task SearchTicker_ReturnsOk_WithMatches()
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
                MatchScore = 0.8m
            }
        };

        _stockMarketService.Setup(service => service.SearchTicker("CSPX", It.IsAny<CancellationToken>()))
            .ReturnsAsync(matches);

        // Act
        var result = await _controller.SearchTicker("CSPX", TestContext.Current.CancellationToken);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var value = Assert.IsType<List<TickerSearchMatch>>(okResult.Value);
        Assert.Single(value);
    }

    [Fact]
    public async Task GetDailyStock_ReturnsOk_WithPrices()
    {
        // Arrange
        var start = new DateTime(2026, 2, 9);
        var end = new DateTime(2026, 2, 10);
        var prices = new List<StockPrice>
        {
            new() { Ticker = "CSPX.LON", PricePerUnit = 747.18m, Currency = DefaultCurrency.USD, Date = end },
            new() { Ticker = "CSPX.LON", PricePerUnit = 747.02m, Currency = DefaultCurrency.USD, Date = start }
        };

        _stockMarketService.Setup(service => service.GetDailyStock("CSPX.LON", start, end, It.IsAny<CancellationToken>()))
            .ReturnsAsync(prices);

        // Act
        var result = await _controller.GetDailyStock("CSPX.LON", start, end, TestContext.Current.CancellationToken);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var value = Assert.IsType<List<StockPrice>>(okResult.Value);
        Assert.Equal(2, value.Count);
    }

    [Fact]
    public async Task GetDailyStock_ReturnsNotFound_WhenEmpty()
    {
        // Arrange
        var start = new DateTime(2026, 2, 9);
        var end = new DateTime(2026, 2, 10);

        _stockMarketService.Setup(service => service.GetDailyStock("CSPX.LON", start, end, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<StockPrice>());

        // Act
        var result = await _controller.GetDailyStock("CSPX.LON", start, end, TestContext.Current.CancellationToken);

        // Assert
        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task GetStocks_ReturnsOk_WithTickers()
    {
        // Arrange
        var stocks = new List<StockDetails>
        {
            new() { Ticker = "CSPX.LON", Name = "Test", Type = "ETF", Region = "UK", Currency = DefaultCurrency.USD }
        };
        _stockDetailsRepository.Setup(repo => repo.GetAll(It.IsAny<CancellationToken>()))
            .ReturnsAsync(stocks);

        // Act
        var result = await _controller.GetStocks(TestContext.Current.CancellationToken);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var value = Assert.IsType<List<StockDetails>>(okResult.Value);
        Assert.Single(value);
    }

    [Fact]
    public async Task AddStock_ReturnsOk_WithPrices()
    {
        // Arrange
        var details = new StockDetails
        {
            Ticker = "CSPX.LON",
            Name = "Test",
            Type = "ETF",
            Region = "UK",
            Currency = DefaultCurrency.USD
        };
        _currencyRepository.Setup(repo => repo.GetOrAdd("USD", "USD", It.IsAny<CancellationToken>()))
            .ReturnsAsync(DefaultCurrency.USD);
        _stockDetailsRepository.Setup(repo => repo.Add(It.IsAny<StockDetails>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(details);

        // Act
        var request = new StockPriceController.AddStockRequest("CSPX.LON", "Test", "ETF", "UK", "USD");
        var result = await _controller.AddStock(request, TestContext.Current.CancellationToken);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var value = Assert.IsType<StockDetails>(okResult.Value);
        Assert.Equal("CSPX.LON", value.Ticker);
    }

    [Fact]
    public async Task DeleteStock_ReturnsNoContent_WhenDeleted()
    {
        // Arrange
        _stockDetailsRepository.Setup(repo => repo.Delete("CSPX.LON", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _controller.DeleteStock("CSPX.LON", TestContext.Current.CancellationToken);

        // Assert
        Assert.IsType<NoContentResult>(result);
    }

    [Fact]
    public async Task GetStockDetails_ReturnsOk_WhenFound()
    {
        // Arrange
        var details = new StockDetails
        {
            Ticker = "CSPX.LON",
            Name = "Test",
            Type = "ETF",
            Region = "UK",
            Currency = DefaultCurrency.USD
        };
        _stockDetailsRepository.Setup(repo => repo.Get("CSPX.LON", It.IsAny<CancellationToken>()))
            .ReturnsAsync(details);

        // Act
        var result = await _controller.GetStockDetails("CSPX.LON", TestContext.Current.CancellationToken);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var value = Assert.IsType<StockDetails>(okResult.Value);
        Assert.Equal("CSPX.LON", value.Ticker);
    }

    [Fact]
    public async Task UpdateStockDetails_ReturnsOk_WhenUpdated()
    {
        // Arrange
        var details = new StockDetails
        {
            Ticker = "CSPX.LON",
            Name = "Test",
            Type = "ETF",
            Region = "UK",
            Currency = DefaultCurrency.USD
        };
        _currencyRepository.Setup(repo => repo.GetOrAdd("USD", "USD", It.IsAny<CancellationToken>()))
            .ReturnsAsync(DefaultCurrency.USD);
        _stockDetailsRepository.Setup(repo => repo.Add(It.IsAny<StockDetails>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(details);

        // Act
        var request = new StockPriceController.UpdateStockRequest("CSPX.LON", "Test", "ETF", "UK", "USD");
        var result = await _controller.UpdateStockDetails(request, TestContext.Current.CancellationToken);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var value = Assert.IsType<StockDetails>(okResult.Value);
        Assert.Equal("CSPX.LON", value.Ticker);
    }
}