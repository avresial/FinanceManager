using FinanceManager.Api.Controllers;
using FinanceManager.Application.FinancialAccounts.Stock.Import;
using FinanceManager.Application.FinancialAccounts.Stock.Market;
using FinanceManager.Application.FinancialAccounts.Stock.Resolution;
using FinanceManager.Domain.Commands.Stocks;
using FinanceManager.Domain.Dtos;
using FinanceManager.Domain.Entities.Currencies;
using FinanceManager.Domain.Entities.Stocks;
using FinanceManager.Domain.Identity.Repositories;
using FinanceManager.Domain.Identity.Services;
using FinanceManager.Domain.Repositories;
using FinanceManager.Domain.Services;
using Microsoft.AspNetCore.Mvc;
using Moq;
using IIsinResolver = FinanceManager.Domain.Services.IIsinResolver;

namespace FinanceManager.Tests.Unit.Api.Controllers;

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
    private readonly Mock<IInstrumentResolver> _instrumentResolverMock = new();
    private readonly StockPriceController _controller;

    public StockPriceControllerTests()
    {
        var isinResolverMock = new Mock<IIsinResolver>();
        isinResolverMock
            .Setup(resolver => resolver.ResolveAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string ticker, string region, CancellationToken ct) =>
            {
                return ticker.ToUpper() switch
                {
                    "CSPX.LON" => "IE00B4L5Y983",
                    "AAPL" => "US0378331005",
                    "MSFT" => "US5949181045",
                    "GOOGL" => "US02079K3059",
                    _ => ticker.Length == 12 ? ticker : "IE00B4L5Y983"
                };
            });
        _controller = new StockPriceController(
            _stockPriceRepository.Object,
            _currencyExchangeService.Object,
            _currencyRepository.Object,
            _stockMarketService.Object,
            _stockPriceProvider.Object,
            _stockDetailsRepository.Object,
            _stockPriceBulkImportService.Object,
            isinResolverMock.Object,
            _instrumentResolverMock.Object);
    }

    [Fact]
    public async Task GetStockPrice_ReturnsLatestOlderPrice_WhenExactDateMissing()
    {
        // Arrange
        var requestedDate = new DateTime(2024, 12, 20, 9, 0, 45, DateTimeKind.Utc);
        var storedDate = requestedDate.AddDays(-1).Date;
        const string isin = "IE00B4L5Y983";
        var storedPrice = new StockPrice
        {
            Isin = isin,
            PricePerUnit = 747.18m,
            Currency = DefaultCurrency.PLN,
            Date = storedDate
        };

        _currencyRepository.Setup(repo => repo.GetCurrency(DefaultCurrency.PLN.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(DefaultCurrency.PLN);
        _stockPriceRepository.Setup(repo => repo.GetThisOrNextOlder(isin, requestedDate))
            .ReturnsAsync(storedPrice);

        // Act
        var result = await _controller.GetStockPrice(isin, DefaultCurrency.PLN.Id, requestedDate, TestContext.Current.CancellationToken);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var value = Assert.IsType<StockPrice>(okResult.Value);
        Assert.Equal(storedDate, value.Date);
        Assert.Equal(747.18m, value.PricePerUnit);
        Assert.Equal(DefaultCurrency.PLN.Symbol, value.Currency.Symbol);
    }

    [Fact]
    public async Task SearchInstrument_ReturnsBadRequest_WhenTickerEmpty()
    {
        var result = await _controller.SearchInstrument("", TestContext.Current.CancellationToken);
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task SearchInstrument_ReturnsNotFound_WhenNoMatch()
    {
        _instrumentResolverMock.Setup(r => r.ResolveAsync("UNKNOWN", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InstrumentResolution { Kind = ResolutionKind.NoMatch });

        var result = await _controller.SearchInstrument("UNKNOWN", TestContext.Current.CancellationToken);

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task SearchInstrument_ReturnsOk_WithAutoResolvedMatch()
    {
        var match = new InstrumentListing("US0378331005", "AAPL", "Apple Inc.", "XNAS", "USD", "Equity");
        _instrumentResolverMock.Setup(r => r.ResolveAsync("AAPL", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InstrumentResolution { Kind = ResolutionKind.AutoResolved, Match = match });

        var result = await _controller.SearchInstrument("AAPL", TestContext.Current.CancellationToken);

        var ok = Assert.IsType<OkObjectResult>(result);
        var resolution = Assert.IsType<InstrumentResolution>(ok.Value);
        Assert.Equal(ResolutionKind.AutoResolved, resolution.Kind);
        Assert.Equal("US0378331005", resolution.Match!.Isin);
    }

    [Fact]
    public async Task SearchInstrument_ReturnsOk_WithAmbiguousCandidates()
    {
        var candidates = new List<InstrumentListing>
        {
            new("IE00B4L5Y983", "CSPX.LON", "iShares S&P 500", "LN", "USD", "ETF"),
            new("IE00B4L5Y984", "CSPX.GY", "iShares S&P 500", "GY", "EUR", "ETF"),
        };
        _instrumentResolverMock.Setup(r => r.ResolveAsync("CSPX", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InstrumentResolution { Kind = ResolutionKind.Ambiguous, Candidates = candidates });

        var result = await _controller.SearchInstrument("CSPX", TestContext.Current.CancellationToken);

        var ok = Assert.IsType<OkObjectResult>(result);
        var resolution = Assert.IsType<InstrumentResolution>(ok.Value);
        Assert.Equal(ResolutionKind.Ambiguous, resolution.Kind);
        Assert.Equal(2, resolution.Candidates.Count);
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
            new() { Isin = "GB0002374006", Ticker = "CSPX.LON", Name = "Test", Type = "ETF", Region = "UK", Currency = DefaultCurrency.USD }
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
            Isin = "GB0002374006",
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
        _stockDetailsRepository.Setup(repo => repo.Delete("IE00B4L5Y983", It.IsAny<CancellationToken>()))
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
            Isin = "IE00B4L5Y983", // Use the ISIN that "CSPX.LON" resolves to
            Ticker = "CSPX.LON",
            Name = "Test",
            Type = "ETF",
            Region = "UK",
            Currency = DefaultCurrency.USD
        };
        _stockDetailsRepository.Setup(repo => repo.Get("IE00B4L5Y983", It.IsAny<CancellationToken>()))
            .ReturnsAsync(details);

        // Act
        var result = await _controller.GetStockDetails("CSPX.LON", TestContext.Current.CancellationToken);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var value = Assert.IsType<StockDetails>(okResult.Value);
        Assert.Equal("CSPX.LON", value.Ticker);
        Assert.Equal("IE00B4L5Y983", value.Isin);
    }

    [Fact]
    public async Task UpdateStockDetails_ReturnsOk_WhenUpdated()
    {
        // Arrange
        var details = new StockDetails
        {
            Isin = "IE00B4L5Y983",
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
        _currencyRepository.Setup(repo => repo.GetOrAdd("USD", "USD", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Currency { Id = 1, Symbol = "USD", ShortName = "USD" });
        var request = new UpdateStockRequest("CSPX.LON", "Test", "ETF", "UK", "USD");
        var result = await _controller.UpdateStockDetails(request, TestContext.Current.CancellationToken);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var value = Assert.IsType<StockDetails>(okResult.Value);
        Assert.Equal("IE00B4L5Y983", value.Isin);
        Assert.Equal("CSPX.LON", value.Ticker);
    }

    [Fact]
    public async Task GetStockPrices_WithDuplicateDatesAndStep_ReturnsLatestPricePerDay()
    {
        // Arrange
        var day = new DateTime(2026, 2, 3, 0, 0, 0, DateTimeKind.Utc);
        const string isin = "US0378331005";
        const string avSymbol = "AAPL";
        var prices = new List<StockPrice>
        {
            new()
            {
                Isin = isin,
                PricePerUnit = 101m,
                Currency = DefaultCurrency.PLN,
                Date = day.AddHours(10)
            },
            new()
            {
                Isin = isin,
                PricePerUnit = 103m,
                Currency = DefaultCurrency.PLN,
                Date = day.AddHours(15)
            }
        };

        _currencyRepository.Setup(repo => repo.GetCurrency(DefaultCurrency.PLN.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(DefaultCurrency.PLN);
        _stockDetailsRepository.Setup(repo => repo.Get(isin, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StockDetails { Isin = isin, Ticker = avSymbol, AlphaVantageSymbol = avSymbol, Currency = DefaultCurrency.PLN });
        _stockMarketService.Setup(service => service.GetStockPrices(avSymbol, day, day.AddHours(23), It.IsAny<CancellationToken>()))
            .ReturnsAsync(prices);

        // Act
        var result = await _controller.GetStockPrices(isin, DefaultCurrency.PLN.Id, day, day.AddHours(23), TimeSpan.FromDays(1).Ticks,
            TestContext.Current.CancellationToken);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var value = Assert.IsType<List<StockPrice>>(okResult.Value);
        var price = Assert.Single(value);
        Assert.Equal(103m, price.PricePerUnit);
        Assert.Equal(day.AddHours(15), price.Date);
    }
}