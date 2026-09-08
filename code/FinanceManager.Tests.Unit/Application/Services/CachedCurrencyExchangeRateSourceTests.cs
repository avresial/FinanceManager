using FinanceManager.Application.FinancialAccounts.Currencies.ExchangeRates;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Currencies.Services;
using Microsoft.Extensions.Caching.Memory;
using Moq;

namespace FinanceManager.Tests.Unit.Application.Services;

[Trait("Category", "Unit")]
public class CachedCurrencyExchangeRateSourceTests : IDisposable
{
    private static readonly Currency _usd = new(1, "USD", "$");
    private static readonly Currency _eur = new(2, "EUR", "€");

    private readonly Mock<ICurrencyExchangeRateSource> _inner = new();
    private readonly MemoryCache _cache = new(new MemoryCacheOptions());

    public void Dispose() => _cache.Dispose();

    [Fact]
    public async Task ResolveAsync_SecondPointCall_UsesCachedExactResult()
    {
        var date = new DateTime(2024, 1, 15);
        _inner
            .Setup(x => x.ResolveAsync(
                _usd,
                _eur,
                It.Is<IReadOnlyCollection<DateTime>>(dates => dates.SequenceEqual(new[] { date })),
                true,
                It.IsAny<CancellationToken>(),
                It.IsAny<CurrencyExchangeRateResolutionContext>()))
            .ReturnsAsync(new Dictionary<DateTime, CurrencyExchangeRateResolution>
            {
                [date] = Resolved(date, 0.92m, CurrencyExchangeRateSource.Provider)
            });
        var sut = new CachedCurrencyExchangeRateSource(_inner.Object, _cache);

        var first = await sut.ResolveAsync(_usd, _eur, [date], reuseCachedMisses: true, ct: TestContext.Current.CancellationToken);
        var second = await sut.ResolveAsync(_usd, _eur, [date], reuseCachedMisses: true, ct: TestContext.Current.CancellationToken);

        Assert.Equal(0.92m, first[date].Result.Value);
        Assert.Equal(0.92m, second[date].Result.Value);
        _inner.Verify(x => x.ResolveAsync(
            _usd,
            _eur,
            It.IsAny<IReadOnlyCollection<DateTime>>(),
            true,
            It.IsAny<CancellationToken>(),
            It.IsAny<CurrencyExchangeRateResolutionContext>()), Times.Once);
    }

    [Fact]
    public async Task ResolveAsync_PointMissIsCached_ButRangeRetriesIt()
    {
        var date = new DateTime(2024, 1, 15);
        _inner
            .SetupSequence(x => x.ResolveAsync(
                _usd,
                _eur,
                It.IsAny<IReadOnlyCollection<DateTime>>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<CurrencyExchangeRateResolutionContext>()))
            .ReturnsAsync(new Dictionary<DateTime, CurrencyExchangeRateResolution>
            {
                [date] = Unavailable(CurrencyExchangeRateStatus.NotYetPublished)
            })
            .ReturnsAsync(new Dictionary<DateTime, CurrencyExchangeRateResolution>
            {
                [date] = Resolved(date, 0.94m, CurrencyExchangeRateSource.Provider)
            });
        var sut = new CachedCurrencyExchangeRateSource(_inner.Object, _cache);

        var point = await sut.ResolveAsync(_usd, _eur, [date], reuseCachedMisses: true, ct: TestContext.Current.CancellationToken);
        var range = await sut.ResolveAsync(_usd, _eur, [date], reuseCachedMisses: false, ct: TestContext.Current.CancellationToken);

        Assert.Equal(CurrencyExchangeRateStatus.NotYetPublished, point[date].Result.Status);
        Assert.Equal(0.94m, range[date].Result.Value);
        _inner.Verify(x => x.ResolveAsync(
            _usd,
            _eur,
            It.IsAny<IReadOnlyCollection<DateTime>>(),
            It.IsAny<bool>(),
            It.IsAny<CancellationToken>(),
            It.IsAny<CurrencyExchangeRateResolutionContext>()), Times.Exactly(2));
    }

    [Fact]
    public async Task ResolveAsync_RangeForwardsOnlyMissingDatesInOneBatch()
    {
        var start = new DateTime(2024, 1, 15);
        var end = start.AddDays(2);
        var missingDate = start.AddDays(1);
        _inner
            .Setup(x => x.ResolveAsync(
                _usd,
                _eur,
                It.Is<IReadOnlyCollection<DateTime>>(dates => dates.SequenceEqual(new[] { missingDate })),
                false,
                It.IsAny<CancellationToken>(),
                It.IsAny<CurrencyExchangeRateResolutionContext>()))
            .ReturnsAsync(new Dictionary<DateTime, CurrencyExchangeRateResolution>
            {
                [missingDate] = Resolved(missingDate, 0.93m, CurrencyExchangeRateSource.Provider)
            });
        var sut = new CachedCurrencyExchangeRateSource(_inner.Object, _cache);

        // Seed two exact entries through the source itself.
        _cache.Set(Key(_usd, _eur, start), Resolved(start, 0.92m, CurrencyExchangeRateSource.Stored));
        _cache.Set(Key(_usd, _eur, end), Resolved(end, 0.94m, CurrencyExchangeRateSource.Stored));

        var result = await sut.ResolveAsync(_usd, _eur, [start, missingDate, end], reuseCachedMisses: false, ct: TestContext.Current.CancellationToken);

        Assert.Equal(3, result.Count);
        Assert.Equal(0.92m, result[start].Result.Value);
        Assert.Equal(0.93m, result[missingDate].Result.Value);
        Assert.Equal(0.94m, result[end].Result.Value);
        _inner.Verify(x => x.ResolveAsync(
            _usd,
            _eur,
            It.IsAny<IReadOnlyCollection<DateTime>>(),
            false,
            It.IsAny<CancellationToken>(),
            It.IsAny<CurrencyExchangeRateResolutionContext>()), Times.Once);
    }

    [Fact]
    public async Task ResolveAsync_RangeDoesNotCacheCarriedForwardOrUnavailableResults()
    {
        var date = new DateTime(2024, 1, 15);
        _inner
            .SetupSequence(x => x.ResolveAsync(
                _usd,
                _eur,
                It.IsAny<IReadOnlyCollection<DateTime>>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<CurrencyExchangeRateResolutionContext>()))
            .ReturnsAsync(new Dictionary<DateTime, CurrencyExchangeRateResolution>
            {
                [date] = new(CurrencyExchangeRateResult.Success(0.92m), CurrencyExchangeRateSource.CarriedForward)
            })
            .ReturnsAsync(new Dictionary<DateTime, CurrencyExchangeRateResolution>
            {
                [date] = Resolved(date, 0.94m, CurrencyExchangeRateSource.Provider)
            });
        var sut = new CachedCurrencyExchangeRateSource(_inner.Object, _cache);

        var range = await sut.ResolveAsync(_usd, _eur, [date], reuseCachedMisses: false, ct: TestContext.Current.CancellationToken);
        var point = await sut.ResolveAsync(_usd, _eur, [date], reuseCachedMisses: true, ct: TestContext.Current.CancellationToken);

        Assert.Equal(CurrencyExchangeRateSource.CarriedForward, range[date].Source);
        Assert.Equal(0.94m, point[date].Result.Value);
        _inner.Verify(x => x.ResolveAsync(
            _usd,
            _eur,
            It.IsAny<IReadOnlyCollection<DateTime>>(),
            It.IsAny<bool>(),
            It.IsAny<CancellationToken>(),
            It.IsAny<CurrencyExchangeRateResolutionContext>()), Times.Exactly(2));
    }

    [Fact]
    public async Task ResolveAsync_NormalizesCurrencyAndDateForCacheKey()
    {
        var date = new DateTime(2024, 1, 15);
        _inner
            .Setup(x => x.ResolveAsync(
                It.IsAny<Currency>(),
                It.IsAny<Currency>(),
                It.IsAny<IReadOnlyCollection<DateTime>>(),
                false,
                It.IsAny<CancellationToken>(),
                It.IsAny<CurrencyExchangeRateResolutionContext>()))
            .ReturnsAsync(new Dictionary<DateTime, CurrencyExchangeRateResolution>
            {
                [date] = Resolved(date, 0.92m, CurrencyExchangeRateSource.Provider)
            });
        var sut = new CachedCurrencyExchangeRateSource(_inner.Object, _cache);

        await sut.ResolveAsync(
            new Currency(1, " usd ", "$"),
            new Currency(2, "eur", "€"),
            [date.AddHours(12)],
            reuseCachedMisses: false,
            ct: TestContext.Current.CancellationToken);
        var second = await sut.ResolveAsync(
            new Currency(1, "USD", "$"),
            new Currency(2, " EUR ", "€"),
            [date.AddHours(19)],
            reuseCachedMisses: false,
            ct: TestContext.Current.CancellationToken);

        Assert.Equal(0.92m, second[date].Result.Value);
        _inner.Verify(x => x.ResolveAsync(
            It.IsAny<Currency>(),
            It.IsAny<Currency>(),
            It.IsAny<IReadOnlyCollection<DateTime>>(),
            false,
            It.IsAny<CancellationToken>(),
            It.IsAny<CurrencyExchangeRateResolutionContext>()), Times.Once);
    }

    private static CurrencyExchangeRateResolution Resolved(
        DateTime date,
        decimal value,
        CurrencyExchangeRateSource source) =>
        new(CurrencyExchangeRateResult.Success(value), source);

    private static CurrencyExchangeRateResolution Unavailable(CurrencyExchangeRateStatus status) =>
        new(status == CurrencyExchangeRateStatus.NotYetPublished
            ? CurrencyExchangeRateResult.NotYetPublished(new DateTime(2024, 1, 15), new DateTime(2024, 1, 16))
            : CurrencyExchangeRateResult.NotFound(), CurrencyExchangeRateSource.Unavailable);

    private static string Key(Currency fromCurrency, Currency toCurrency, DateTime date) =>
        $"EXCHANGE_RATE_RESULT_{fromCurrency.ShortName.Trim().ToUpperInvariant()}_{toCurrency.ShortName.Trim().ToUpperInvariant()}_{date:yyyyMMdd}";
}