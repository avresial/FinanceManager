using FinanceManager.Application.FinancialAccounts.Currencies.ExchangeRates;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Currencies.Services;
using Microsoft.Extensions.Caching.Memory;
using Moq;

namespace FinanceManager.Tests.Unit.Application.Services;

[Trait("Category", "Unit")]
public class CachedCurrencyExchangeServiceTests : IDisposable
{
    private static readonly Currency _usd = new(1, "USD", "$");
    private static readonly Currency _eur = new(2, "EUR", "€");

    private readonly Mock<ICurrencyExchangeService> _inner = new();
    private readonly MemoryCache _cache = new(new MemoryCacheOptions());

    private CachedCurrencyExchangeService CreateSut() => new(_inner.Object, _cache);

    public void Dispose() => _cache.Dispose();

    [Fact]
    public async Task GetExchangeRate_SecondCall_ServedFromCache()
    {
        var date = new DateTime(2024, 1, 15);
        _inner.Setup(x => x.GetExchangeRateAsync(_usd, _eur, date)).ReturnsAsync(0.92m);
        var sut = CreateSut();

        var first = await sut.GetExchangeRateAsync(_usd, _eur, date);
        var second = await sut.GetExchangeRateAsync(_usd, _eur, date);

        Assert.Equal(0.92m, first);
        Assert.Equal(0.92m, second);
        _inner.Verify(x => x.GetExchangeRateAsync(_usd, _eur, date), Times.Once);
    }

    [Fact]
    public async Task GetExchangeRate_MissIsCached_SoRepeatedLookupsDoNotHitInnerAgain()
    {
        var date = new DateTime(2024, 1, 15);
        _inner.Setup(x => x.GetExchangeRateAsync(_usd, _eur, date)).ReturnsAsync((decimal?)null);
        var sut = CreateSut();

        var first = await sut.GetExchangeRateAsync(_usd, _eur, date);
        var second = await sut.GetExchangeRateAsync(_usd, _eur, date);

        Assert.Null(first);
        Assert.Null(second);
        _inner.Verify(x => x.GetExchangeRateAsync(_usd, _eur, date), Times.Once);
    }
}