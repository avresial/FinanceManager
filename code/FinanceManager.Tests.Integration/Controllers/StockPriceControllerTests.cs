using FinanceManager.Application.FinancialAccounts.Stock.Resolution;
using FinanceManager.Components.HttpClients;
using FinanceManager.Domain.Enums;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Currencies.Services;
using FinanceManager.Domain.FinancialAccounts.Shared.Services;
using FinanceManager.Domain.FinancialAccounts.Stock.Entities;
using FinanceManager.Domain.FinancialAccounts.Stock.Services;
using FinanceManager.Domain.Identity.Services;
using FinanceManager.Infrastructure.Contexts;
using FinanceManager.Infrastructure.Dtos;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace FinanceManager.Tests.Integration.Controllers;

[Collection("api")]
[Trait("Category", "Integration")]
public class StockPriceControllerTests(OptionsProvider optionsProvider) : ControllerTests(optionsProvider), IDisposable
{
    private const string _aaplIsin = "US0378331005";
    private TestDatabase? _testDatabase;
    Mock<ICurrencyExchangeService> _currencyExchangeMock = new();
    Mock<IIsinResolver> _isinResolverMock = new();
    Mock<IInstrumentResolver> _instrumentResolverMock = new();

    protected override void ConfigureServices(IServiceCollection services)
    {
        _testDatabase = new TestDatabase();

        var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
        if (descriptor != null)
            services.Remove(descriptor);

        services.AddSingleton(_testDatabase!.Context);

        _currencyExchangeMock.Setup(x => x.GetExchangeRateAsync(It.IsAny<Currency>(), It.IsAny<Currency>(), It.IsAny<DateTime>()))
            .ReturnsAsync(1.1m);

        services.AddSingleton(_currencyExchangeMock.Object);

        _isinResolverMock.Setup(x => x.ResolveAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string ticker, string? _, CancellationToken _) =>
                string.Equals(ticker?.Trim(), "AAPL", StringComparison.OrdinalIgnoreCase) ? _aaplIsin : null);

        var isinResolverDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IIsinResolver));
        if (isinResolverDescriptor != null)
            services.Remove(isinResolverDescriptor);
        services.AddSingleton(_isinResolverMock.Object);

        _instrumentResolverMock.Setup(x => x.ResolveAsync("AAPL", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InstrumentResolution
            {
                Kind = ResolutionKind.AutoResolved,
                Match = new InstrumentListing(_aaplIsin, "AAPL", "Apple Inc.", "XNAS", "USD", "Equity")
            });
        _instrumentResolverMock.Setup(x => x.ResolveAsync("UNKNOWN", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InstrumentResolution { Kind = ResolutionKind.NoMatch });

        var instrumentResolverDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IInstrumentResolver));
        if (instrumentResolverDescriptor != null)
            services.Remove(instrumentResolverDescriptor);
        services.AddSingleton(_instrumentResolverMock.Object);
    }

    private async Task SeedWithTestStockPrice(string ticker = "AAPL", decimal price = 100, Currency? currency = null, DateTime date = default)
    {
        currency ??= DefaultCurrency.PLN;
        if (date == default) date = DateTime.UtcNow.Date;

        if (await _testDatabase!.Context.StockPrices
            .Include(x => x.StockDetails)
            .AnyAsync(x => x.StockDetails!.Ticker == ticker && x.Date == date, TestContext.Current.CancellationToken))
            return;

        var stockDetails = await _testDatabase!.Context.StockDetails
            .Include(x => x.Currency)
            .FirstOrDefaultAsync(x => x.Ticker == ticker, TestContext.Current.CancellationToken);

        if (stockDetails is null)
        {
            stockDetails = new StockDetails
            {
                Isin = "US0378331005",
                Ticker = ticker,
                Name = "Test",
                Type = "Stock",
                Region = "US",
                Currency = currency
            };
            _testDatabase.Context.StockDetails.Add(stockDetails);
        }

        _testDatabase!.Context.StockPrices.Add(new StockPriceDto
        {
            PricePerUnit = price,
            StockDetails = stockDetails,
            Date = date
        });

        await _testDatabase.Context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task AddStockPrice_AddsPrice()
    {
        Authorize("TestUser", 1, UserRole.User);

        await new StockPriceHttpClient(Client, null!).AddStockPrice(_aaplIsin, 150, 1, DateTime.UtcNow, TestContext.Current.CancellationToken);

        var result = await new StockPriceHttpClient(Client, null!).GetStockPrice(_aaplIsin, 1, DateTime.UtcNow, TestContext.Current.CancellationToken);
        Assert.NotNull(result);
        Assert.Equal(150, result.PricePerUnit);
    }

    [Fact]
    public async Task UpdateStockPrice_UpdatesPrice()
    {
        _currencyExchangeMock.Setup(x => x.GetExchangeRateAsync(It.IsAny<Currency>(), It.IsAny<Currency>(), It.IsAny<DateTime>()))
         .ReturnsAsync(1);

        await SeedWithTestStockPrice("AAPL", 100);
        Authorize("TestUser", 1, UserRole.User);

        await new StockPriceHttpClient(Client, null!).UpdateStockPrice(_aaplIsin, 200, 0, DateTime.UtcNow, TestContext.Current.CancellationToken);

        var result = await new StockPriceHttpClient(Client, null!).GetStockPrice(_aaplIsin, 0, DateTime.UtcNow, TestContext.Current.CancellationToken);
        Assert.NotNull(result);
        Assert.Equal(200, result.PricePerUnit);
    }

    [Fact]
    public async Task GetStockPrice_ReturnsPrice()
    {
        _currencyExchangeMock.Setup(x => x.GetExchangeRateAsync(It.IsAny<Currency>(), It.IsAny<Currency>(), It.IsAny<DateTime>()))
         .ReturnsAsync(1);

        await SeedWithTestStockPrice();

        var result = await new StockPriceHttpClient(Client, null!).GetStockPrice(_aaplIsin, 0, DateTime.UtcNow, TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Equal(_aaplIsin, result.Isin);
        Assert.Equal(100, result.PricePerUnit);
    }

    [Fact]
    public async Task GetStockPrice_WithExchange_ReturnsConvertedPrice()
    {
        await SeedWithTestStockPrice();

        var result = await new StockPriceHttpClient(Client, null!).GetStockPrice(_aaplIsin, 1, DateTime.UtcNow, TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Equal(110, result.PricePerUnit);
    }

    [Fact]
    public async Task GetStockPrice_ReturnsLatestOlderPrice_WhenExactDateMissing()
    {
        _currencyExchangeMock.Setup(x => x.GetExchangeRateAsync(It.IsAny<Currency>(), It.IsAny<Currency>(), It.IsAny<DateTime>()))
         .ReturnsAsync(1);

        var storedDate = new DateTime(2024, 12, 19, 0, 0, 0, DateTimeKind.Utc);
        var requestedDate = new DateTime(2024, 12, 20, 9, 0, 45, DateTimeKind.Utc);

        await SeedWithTestStockPrice("AAPL", 100, DefaultCurrency.PLN, storedDate);

        var result = await new StockPriceHttpClient(Client, null!).GetStockPrice(_aaplIsin, 0, requestedDate, TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Equal(storedDate.Date, result.Date.Date);
        Assert.Equal(100, result.PricePerUnit);
    }

    [Fact]
    public async Task GetStockPrices_ReturnsList()
    {
        await SeedWithTestStockPrice();

        var result = await new StockPriceHttpClient(Client, null!).GetStockPrices(_aaplIsin, DefaultCurrency.PLN.Id, DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(1), TimeSpan.FromDays(1), TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.NotEmpty(result);
    }

    [Fact]
    public async Task SearchInstrument_ReturnsAutoResolvedMatch()
    {
        var result = await new StockPriceHttpClient(Client, null!).SearchInstrument("AAPL", TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Equal(ResolutionKind.AutoResolved, result.Kind);
        Assert.NotNull(result.Match);
        Assert.Equal(_aaplIsin, result.Match.Isin);
    }

    [Fact]
    public async Task SearchInstrument_ReturnsNull_WhenNoMatch()
    {
        var result = await new StockPriceHttpClient(Client, null!).SearchInstrument("UNKNOWN", TestContext.Current.CancellationToken);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetStockPrice_ByIsin_ReturnsPrice()
    {
        _currencyExchangeMock.Setup(x => x.GetExchangeRateAsync(It.IsAny<Currency>(), It.IsAny<Currency>(), It.IsAny<DateTime>()))
            .ReturnsAsync(1);

        await SeedWithTestStockPrice("AAPL", 250);

        var result = await new StockPriceHttpClient(Client, null!).GetStockPrice(_aaplIsin, 0, DateTime.UtcNow, TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Equal(_aaplIsin, result.Isin);
        Assert.Equal(250, result.PricePerUnit);
    }

    [Fact]
    public async Task GetLatestMissingStockPrice_ReturnsDate()
    {
        var uteNow = DateTime.UtcNow;
        await SeedWithTestStockPrice("AAPL", 100, DefaultCurrency.USD, uteNow.AddDays(-2).Date);
        // No auth

        var result = await new StockPriceHttpClient(Client, null!).GetLatestMissingStockPrice("AAPL");

        Assert.NotNull(result);
        Assert.Equal(uteNow.Date, result);
    }

    [Fact]
    public async Task GetStockDetails_ReturnsCurrency()
    {
        await SeedWithTestStockPrice();
        Authorize("TestUser", 1, UserRole.Admin);

        var result = await new StockPriceHttpClient(Client, null!).GetStockDetails("AAPL", TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Equal("AAPL", result.Ticker);
        Assert.Equal(DefaultCurrency.PLN.Symbol, result.Currency.Symbol);
    }

    [Fact]
    public async Task GetStocks_AsAdmin_ReturnsStocks()
    {
        await SeedWithTestStockPrice("AAPL", 100, DefaultCurrency.PLN);
        Authorize("TestUser", 1, UserRole.Admin);

        var result = await new StockPriceHttpClient(Client, null!).GetStocks(TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.NotEmpty(result);
        Assert.Contains(result, x => x.Ticker == "AAPL");
    }

    [Fact]
    public async Task DeleteStockPrice_AsAdmin_RemovesPrice()
    {
        var date = DateTime.UtcNow.Date;
        await SeedWithTestStockPrice("AAPL", 100, DefaultCurrency.PLN, date);

        var dto = await _testDatabase!.Context.StockPrices
            .Include(x => x.StockDetails)
            .FirstAsync(x => x.StockDetails!.Ticker == "AAPL" && x.Date == date, TestContext.Current.CancellationToken);

        Authorize("TestUser", 1, UserRole.Admin);

        var ok = await new StockPriceHttpClient(Client, null!).DeleteStockPrice(dto.Id, TestContext.Current.CancellationToken);
        Assert.True(ok);

        var exists = await _testDatabase.Context.StockPrices.AnyAsync(x => x.Id == dto.Id, TestContext.Current.CancellationToken);
        Assert.False(exists);
    }

    public override void Dispose()
    {
        base.Dispose();
        if (_testDatabase is null)
            return;

        _testDatabase.Dispose();
        _testDatabase = null;
        GC.SuppressFinalize(this);
    }
}