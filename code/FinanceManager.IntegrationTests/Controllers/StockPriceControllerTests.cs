using FinanceManager.Components.HttpClients;
using FinanceManager.Domain.Entities.Currencies;
using FinanceManager.Domain.Enums;
using FinanceManager.Domain.Services;
using FinanceManager.Infrastructure.Contexts;
using FinanceManager.Infrastructure.Dtos;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace FinanceManager.IntegrationTests.Controllers;

[Collection("api")]
[Trait("Category", "Integration")]
public class StockPriceControllerTests(OptionsProvider optionsProvider) : ControllerTests(optionsProvider), IDisposable
{
    private TestDatabase? _testDatabase;
    Mock<ICurrencyExchangeService> _currencyExchangeMock = new();

    protected override void ConfigureServices(IServiceCollection services)
    {
        _testDatabase = new TestDatabase();

        // remove any registration for AppDbContext
        var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
        if (descriptor != null)
            services.Remove(descriptor);

        services.AddSingleton(_testDatabase!.Context);

        _currencyExchangeMock.Setup(x => x.GetExchangeRateAsync(It.IsAny<Currency>(), It.IsAny<Currency>(), It.IsAny<DateTime>()))
            .ReturnsAsync(1.1m);

        services.AddSingleton(_currencyExchangeMock.Object);
    }

    private async Task SeedWithTestStockPrice(string ticker = "AAPL", decimal price = 100, Currency? currency = null, DateTime date = default)
    {
        currency ??= DefaultCurrency.PLN;
        if (date == default) date = DateTime.UtcNow.Date;

        if (await _testDatabase!.Context.StockPrices.AnyAsync(x => x.Ticker == ticker && x.Date == date))
            return;

        _testDatabase!.Context.StockPrices.Add(new StockPriceDto
        {
            Ticker = ticker,
            PricePerUnit = price,
            Currency = currency,
            Date = date
        });

        await _testDatabase.Context.SaveChangesAsync();
    }

    [Fact]
    public async Task AddStockPrice_AddsPrice()
    {
        Authorize("TestUser", 1, UserRole.User);

        await new StockPriceHttpClient(Client, null!).AddStockPrice("AAPL", 150, 1, DateTime.UtcNow);

        // Verify by getting it
        var result = await new StockPriceHttpClient(Client, null!).GetStockPrice("AAPL", 1, DateTime.UtcNow);
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

        await new StockPriceHttpClient(Client, null!).UpdateStockPrice("AAPL", 200, 0, DateTime.UtcNow);

        var result = await new StockPriceHttpClient(Client, null!).GetStockPrice("AAPL", 0, DateTime.UtcNow);
        Assert.NotNull(result);
        Assert.Equal(200, result.PricePerUnit);
    }

    [Fact]
    public async Task GetStockPrice_ReturnsPrice()
    {
        _currencyExchangeMock.Setup(x => x.GetExchangeRateAsync(It.IsAny<Currency>(), It.IsAny<Currency>(), It.IsAny<DateTime>()))
         .ReturnsAsync(1);

        await SeedWithTestStockPrice();
        // No auth needed

        var result = await new StockPriceHttpClient(Client, null!).GetStockPrice("AAPL", 0, DateTime.UtcNow);

        Assert.NotNull(result);
        Assert.Equal("AAPL", result.Ticker);
        Assert.Equal(100, result.PricePerUnit);
    }

    [Fact]
    public async Task GetStockPrice_WithExchange_ReturnsConvertedPrice()
    {
        await SeedWithTestStockPrice();
        // Mock returns 1.1, so 100 * 1.1 = 110

        var result = await new StockPriceHttpClient(Client, null!).GetStockPrice("AAPL", 1, DateTime.UtcNow);

        Assert.NotNull(result);
        Assert.Equal(110, result.PricePerUnit);
    }

    [Fact]
    public async Task GetStockPrices_ReturnsList()
    {
        await SeedWithTestStockPrice();
        // No auth

        var result = await new StockPriceHttpClient(Client, null!).GetStockPrices("AAPL", DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(1), TimeSpan.FromDays(1));

        Assert.NotNull(result);
        Assert.NotEmpty(result);
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
    public async Task GetTickerCurrency_ReturnsCurrency()
    {
        await SeedWithTestStockPrice();
        // No auth

        var result = await new StockPriceHttpClient(Client, null!).GetTickerCurrency("AAPL");

        Assert.NotNull(result);
        Assert.Equal("AAPL", result.Ticker);
        Assert.Equal(DefaultCurrency.PLN.Symbol, result.Currency.Symbol);
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