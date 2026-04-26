using FinanceManager.Api.Controllers;
using FinanceManager.Application.Services.Stocks;
using FinanceManager.Domain.Commands.Stocks;
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
    private readonly Mock<IStockPriceProvider> _stockPriceProvider = new();
    private readonly Mock<IStockDetailsRepository> _stockDetailsRepository = new();
    private readonly Mock<IStockPriceBulkImportService> _stockPriceBulkImportService = new();
    private readonly StockPriceController _controller;

    public StockPriceControllerTests()
    {
        _controller = new StockPriceController(
            _stockPriceRepository.Object,
            _currencyExchangeService.Object,
            _currencyRepository.Object,
            _stockMarketService.Object,
                _stockPriceProvider.Object,
                _stockDetailsRepository.Object,
                _stockPriceBulkImportService.Object);
    }

    [Fact]
    public async Task GetStockPrice_ReturnsLatestOlderPrice_WhenExactDateMissing()
    {
        // Arrange
        var requestedDate = new DateTime(2024, 12, 20, 9, 0, 45, DateTimeKind.Utc);
        var storedDate = requestedDate.AddDays(-1).Date;
        var storedPrice = new StockPrice
        {
            Ticker = "CSPX.LON",
            PricePerUnit = 747.18m,
            Currency = DefaultCurrency.PLN,
            Date = storedDate
        };

        _currencyRepository.Setup(repo => repo.GetCurrency(DefaultCurrency.PLN.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(DefaultCurrency.PLN);
        _stockPriceRepository.Setup(repo => repo.GetThisOrNextOlder("CSPX.LON", requestedDate))
            .ReturnsAsync(storedPrice);

        // Act
        var result = await _controller.GetStockPrice("CSPX.LON", DefaultCurrency.PLN.Id, requestedDate, TestContext.Current.CancellationToken);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var value = Assert.IsType<StockPrice>(okResult.Value);
        Assert.Equal(storedDate, value.Date);
        Assert.Equal(747.18m, value.PricePerUnit);
        Assert.Equal(DefaultCurrency.PLN.Symbol, value.Currency.Symbol);
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
        var result = await _controller.GetStocksDetails(TestContext.Current.CancellationToken);

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
        var request = new AddStockRequest("CSPX.LON", "Test", "ETF", "UK", "USD");
        var result = await _controller.AddStockDetails(request, TestContext.Current.CancellationToken);

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
    public async Task DeleteStockPrice_ReturnsNoContent_WhenDeleted()
    {
        // Arrange
        _stockPriceRepository.Setup(repo => repo.Delete(123, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _controller.DeleteStockPrice(123, TestContext.Current.CancellationToken);

        // Assert
        Assert.IsType<NoContentResult>(result);
    }

    [Fact]
    public async Task DeleteStockPrice_ReturnsNotFound_WhenMissing()
    {
        // Arrange
        _stockPriceRepository.Setup(repo => repo.Delete(999, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var result = await _controller.DeleteStockPrice(999, TestContext.Current.CancellationToken);

        // Assert
        Assert.IsType<NotFoundResult>(result);
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
        var request = new UpdateStockRequest("CSPX.LON", "Test", "ETF", "UK", "USD");
        var result = await _controller.UpdateStockDetails(request, TestContext.Current.CancellationToken);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var value = Assert.IsType<StockDetails>(okResult.Value);
        Assert.Equal("CSPX.LON", value.Ticker);
    }

    [Fact]
    public async Task GetStockPrices_WithDuplicateDatesAndStep_ReturnsLatestPricePerDay()
    {
        // Arrange
        var day = new DateTime(2026, 2, 3, 0, 0, 0, DateTimeKind.Utc);
        var prices = new List<StockPrice>
        {
            new()
            {
                Ticker = "AAPL",
                PricePerUnit = 101m,
                Currency = DefaultCurrency.PLN,
                Date = day.AddHours(10)
            },
            new()
            {
                Ticker = "AAPL",
                PricePerUnit = 103m,
                Currency = DefaultCurrency.PLN,
                Date = day.AddHours(15)
            }
        };

        _currencyRepository.Setup(repo => repo.GetCurrency(DefaultCurrency.PLN.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(DefaultCurrency.PLN);
        _stockMarketService.Setup(service => service.GetStockPrices("AAPL", day, day.AddHours(23), It.IsAny<CancellationToken>()))
            .ReturnsAsync(prices);

        // Act
        var result = await _controller.GetStockPrices("AAPL", DefaultCurrency.PLN.Id, day, day.AddHours(23), TimeSpan.FromDays(1).Ticks,
            TestContext.Current.CancellationToken);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var value = Assert.IsType<List<StockPrice>>(okResult.Value);
        var price = Assert.Single(value);
        Assert.Equal(103m, price.PricePerUnit);
        Assert.Equal(day.AddHours(15), price.Date);
    }
}