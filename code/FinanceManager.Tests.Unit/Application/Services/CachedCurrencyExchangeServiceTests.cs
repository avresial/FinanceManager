using FinanceManager.Application.FinancialAccounts.Currencies.ExchangeRates;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Currencies.Services;
using FinanceManager.Domain.FinancialAccounts.Stock.Entities;
using Microsoft.Extensions.Caching.Memory;
using Moq;

namespace FinanceManager.Tests.Unit.Application.Services;

[Collection("Application")]
[Trait("Category", "Unit")]
public class CachedCurrencyExchangeServiceTests
{
    [Fact]
    public async Task GetPricePerUnit_RepeatedCalls_CachesUnderlyingRate()
    {
        // Arrange
        var from = new Currency(1, "USD", "$");
        var to = new Currency(2, "EUR", "€");
        var date = new DateTime(2024, 1, 15);
        var stockPrice = new StockPrice { Isin = "US0000000001", PricePerUnit = 100m, Currency = from };

        var inner = new Mock<ICurrencyExchangeService>();
        inner.Setup(x => x.GetExchangeRateAsync(from, to, date.Date)).ReturnsAsync(0.9m);

        using var cache = new MemoryCache(new MemoryCacheOptions());
        var service = new CachedCurrencyExchangeService(inner.Object, cache);

        // Act
        var first = await service.GetPricePerUnit(stockPrice, to, date);
        var second = await service.GetPricePerUnit(stockPrice, to, date);

        // Assert
        Assert.Equal(90m, first);
        Assert.Equal(90m, second);
        inner.Verify(x => x.GetExchangeRateAsync(from, to, date.Date), Times.Once);
    }

    [Fact]
    public async Task GetPricePerUnit_SameCurrency_DoesNotHitInner()
    {
        // Arrange
        var currency = new Currency(1, "USD", "$");
        var stockPrice = new StockPrice { Isin = "US0378331005", PricePerUnit = 150.75m, Currency = currency };
        var date = new DateTime(2024, 1, 15);

        var inner = new Mock<ICurrencyExchangeService>();
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var service = new CachedCurrencyExchangeService(inner.Object, cache);

        // Act
        var result = await service.GetPricePerUnit(stockPrice, currency, date);

        // Assert
        Assert.Equal(150.75m, result);
        inner.Verify(
            x => x.GetExchangeRateAsync(It.IsAny<Currency>(), It.IsAny<Currency>(), It.IsAny<DateTime>()),
            Times.Never);
    }

    [Fact]
    public async Task GetPricePerUnit_NullStockPrice_ReturnsNull()
    {
        // Arrange
        var currency = new Currency(1, "USD", "$");
        var date = new DateTime(2024, 1, 15);

        var inner = new Mock<ICurrencyExchangeService>();
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var service = new CachedCurrencyExchangeService(inner.Object, cache);

        // Act
        var result = await service.GetPricePerUnit(null!, currency, date);

        // Assert
        Assert.Null(result);
    }
}