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
        _inner.Setup(x => x.GetExchangeRateResultAsync(_usd, _eur, date)).ReturnsAsync(CurrencyExchangeRateResult.Success(0.92m));
        var sut = CreateSut();

        var first = await sut.GetExchangeRateAsync(_usd, _eur, date);
        var second = await sut.GetExchangeRateAsync(_usd, _eur, date);

        Assert.Equal(0.92m, first);
        Assert.Equal(0.92m, second);
        _inner.Verify(x => x.GetExchangeRateResultAsync(_usd, _eur, date), Times.Once);
    }

    [Fact]
    public async Task GetExchangeRate_MissIsCached_SoRepeatedLookupsDoNotHitInnerAgain()
    {
        var date = new DateTime(2024, 1, 15);
        _inner.Setup(x => x.GetExchangeRateResultAsync(_usd, _eur, date)).ReturnsAsync(CurrencyExchangeRateResult.NotFound());
        var sut = CreateSut();

        var first = await sut.GetExchangeRateAsync(_usd, _eur, date);
        var second = await sut.GetExchangeRateAsync(_usd, _eur, date);

        Assert.Null(first);
        Assert.Null(second);
        _inner.Verify(x => x.GetExchangeRateResultAsync(_usd, _eur, date), Times.Once);
    }

    [Fact]
    public async Task GetExchangeRateResult_PendingPublicationIsCachedWithoutRetrying()
    {
        var date = new DateTime(2024, 1, 15);
        var result = CurrencyExchangeRateResult.NotYetPublished(date, date.AddDays(1));
        _inner.Setup(x => x.GetExchangeRateResultAsync(_usd, _eur, date)).ReturnsAsync(result);
        var sut = CreateSut();

        var first = await sut.GetExchangeRateResultAsync(_usd, _eur, date);
        var second = await sut.GetExchangeRateResultAsync(_usd, _eur, date);

        Assert.Equal(result, first);
        Assert.Equal(result, second);
        _inner.Verify(x => x.GetExchangeRateResultAsync(_usd, _eur, date), Times.Once);
    }

    [Fact]
    public async Task GetExchangeRateAsync_Range_CachesResolvedEntriesForSubsequentPointLookups()
    {
        var start = new DateTime(2024, 1, 15);
        var end = new DateTime(2024, 1, 16);
        var date1 = start;
        var date2 = end;

        var inner = new RangeAwareInner
        {
            RangeResults = [
                (date1, 0.92m, CurrencyExchangeRateSource.Provider),
                (date2, 0.93m, CurrencyExchangeRateSource.Provider)
            ]
        };

        var sut = new CachedCurrencyExchangeService(inner, _cache);

        var rangeRates = await sut.GetExchangeRateAsync(_usd, _eur, start, end);

        Assert.Equal(2, rangeRates.Count);
        Assert.Equal((date1, (decimal?)0.92m), rangeRates[0]);
        Assert.Equal((date2, (decimal?)0.93m), rangeRates[1]);

        var point1 = await sut.GetExchangeRateAsync(_usd, _eur, date1);
        var pointResult2 = await sut.GetExchangeRateResultAsync(_usd, _eur, date2);

        Assert.Equal(0.92m, point1);
        Assert.True(pointResult2.IsSuccess);
        Assert.Equal(0.93m, pointResult2.Value);

        Assert.Equal(0, inner.PointCalls);
    }

    [Fact]
    public async Task GetExchangeRateAsync_Range_DoesNotCacheCarriedForwardOrUnsuccessfulEntriesForPointLookups()
    {
        var start = new DateTime(2024, 1, 15);
        var end = new DateTime(2024, 1, 17);
        var date1 = start;
        var date2 = start.AddDays(1);
        var date3 = end;

        var inner = new RangeAwareInner
        {
            RangeResults = [
                (date1, 0.92m, CurrencyExchangeRateSource.Provider),
                (date2, 0.92m, CurrencyExchangeRateSource.CarriedForward),
                (date3, null, CurrencyExchangeRateSource.Unavailable)
            ],
            PointResult = (_, _, date) => date == date2
                ? CurrencyExchangeRateResult.Success(0.94m)
                : CurrencyExchangeRateResult.NotFound()
        };

        var sut = new CachedCurrencyExchangeService(inner, _cache);

        var rangeRates = await sut.GetExchangeRateAsync(_usd, _eur, start, end);
        Assert.Equal(3, rangeRates.Count);

        // date1 was provider-supplied -> served from cache
        var point1 = await sut.GetExchangeRateAsync(_usd, _eur, date1);
        Assert.Equal(0.92m, point1);
        Assert.Equal(0, inner.PointCallsFor(date1));

        // date2 was carried forward -> cache miss, resolves via inner
        var point2 = await sut.GetExchangeRateAsync(_usd, _eur, date2);
        Assert.Equal(0.94m, point2);
        Assert.Equal(1, inner.PointCallsFor(date2));

        // date3 was null / missing -> cache miss, resolves via inner
        var point3 = await sut.GetExchangeRateResultAsync(_usd, _eur, date3);
        Assert.Equal(CurrencyExchangeRateStatus.NotFound, point3.Status);
        Assert.Equal(1, inner.PointCallsFor(date3));
    }

    [Fact]
    public async Task GetExchangeRateAsync_Range_NormalizesCurrencyCasingAndWhitespaceForPointLookupCacheHit()
    {
        var date = new DateTime(2024, 1, 15);
        var lowerUsd = new Currency(1, "usd", "$");
        var lowerEur = new Currency(2, "eur", "€");

        var inner = new RangeAwareInner
        {
            RangeResults = [(date, 0.92m, CurrencyExchangeRateSource.Provider)]
        };
        var sut = new CachedCurrencyExchangeService(inner, _cache);

        await sut.GetExchangeRateAsync(lowerUsd, lowerEur, date, date);

        var spacedUsd = new Currency(1, " USD ", "$");
        var spacedEur = new Currency(2, " eur ", "€");

        var point = await sut.GetExchangeRateAsync(spacedUsd, spacedEur, date);
        Assert.Equal(0.92m, point);

        Assert.Equal(0, inner.PointCalls);
    }

    [Fact]
    public async Task GetExchangeRateAsync_Range_FallsBackSafelyWhenInnerLacksRangeProvenanceContract()
    {
        var start = new DateTime(2024, 1, 15);
        var end = new DateTime(2024, 1, 16);
        var date1 = start;
        var date2 = end;

        _inner
            .Setup(x => x.GetExchangeRateAsync(_usd, _eur, start, end))
            .ReturnsAsync([(date1, 0.92m), (date2, 0.93m)]);

        _inner
            .Setup(x => x.GetExchangeRateResultAsync(_usd, _eur, date1))
            .ReturnsAsync(CurrencyExchangeRateResult.Success(0.92m));

        var sut = CreateSut();

        var rangeRates = await sut.GetExchangeRateAsync(_usd, _eur, start, end);
        Assert.Equal(2, rangeRates.Count);
        Assert.Equal(0.92m, rangeRates[0].Value);

        // Because inner did not implement ICurrencyExchangeRateResolutionService, no point entries were cached
        var point = await sut.GetExchangeRateAsync(_usd, _eur, date1);
        Assert.Equal(0.92m, point);
        _inner.Verify(x => x.GetExchangeRateResultAsync(_usd, _eur, date1), Times.Once);
    }

    [Fact]
    public async Task GetExchangeRateRangeWithProvenanceAsync_ReturnsProvenanceAndCachesResolvedEntries()
    {
        var start = new DateTime(2024, 1, 15);
        var end = new DateTime(2024, 1, 16);
        var date1 = start;
        var date2 = end;

        var inner = new RangeAwareInner
        {
            RangeResults = [
                (date1, 0.92m, CurrencyExchangeRateSource.Provider),
                (date2, 0.92m, CurrencyExchangeRateSource.CarriedForward)
            ]
        };

        var sut = new CachedCurrencyExchangeService(inner, _cache);

        var provenanced = await sut.GetExchangeRateRangeWithProvenanceAsync(_usd, _eur, start, end);
        Assert.Equal(2, provenanced.Count);
        Assert.Equal(CurrencyExchangeRateSource.Provider, provenanced[0].Source);
        Assert.Equal(CurrencyExchangeRateSource.CarriedForward, provenanced[1].Source);

        // date1 should be cached for point lookup
        var point1 = await sut.GetExchangeRateAsync(_usd, _eur, date1);
        Assert.Equal(0.92m, point1);
        Assert.Equal(0, inner.PointCallsFor(date1));
    }

    [Fact]
    public async Task GetExchangeRateRangeWithProvenanceAsync_FallsBackSafelyWhenInnerLacksRangeProvenanceContract()
    {
        var start = new DateTime(2024, 1, 15);
        var end = new DateTime(2024, 1, 16);

        _inner
            .Setup(x => x.GetExchangeRateAsync(_usd, _eur, start, end))
            .ReturnsAsync([(start, 0.92m), (end, 0.93m)]);

        var sut = CreateSut();

        var provenanced = await sut.GetExchangeRateRangeWithProvenanceAsync(_usd, _eur, start, end);
        Assert.Equal(2, provenanced.Count);
        Assert.Equal(CurrencyExchangeRateSource.Unknown, provenanced[0].Source);
        Assert.Equal(CurrencyExchangeRateSource.Unknown, provenanced[1].Source);
    }

    [Theory]
    [InlineData((int)CurrencyExchangeRateSource.Stored, true)]
    [InlineData((int)CurrencyExchangeRateSource.Provider, true)]
    [InlineData((int)CurrencyExchangeRateSource.SameCurrency, true)]
    [InlineData((int)CurrencyExchangeRateSource.DerivedViaUsd, false)]
    [InlineData((int)CurrencyExchangeRateSource.CarriedForward, false)]
    [InlineData((int)CurrencyExchangeRateSource.Unavailable, false)]
    [InlineData((int)CurrencyExchangeRateSource.Unknown, false)]
    public async Task GetExchangeRateAsync_Range_ReusesOnlyResolvedDailySources(
        int sourceValue,
        bool shouldReuse)
    {
        var source = (CurrencyExchangeRateSource)sourceValue;
        var date = new DateTime(2024, 1, 15);
        var inner = new RangeAwareInner
        {
            RangeResults = [(date, 0.92m, source)],
            PointResult = (_, _, _) => CurrencyExchangeRateResult.Success(0.94m)
        };
        var sut = new CachedCurrencyExchangeService(inner, _cache);

        var range = await sut.GetExchangeRateRangeWithProvenanceAsync(_usd, _eur, date, date);
        var point = await sut.GetExchangeRateAsync(_usd, _eur, date);

        Assert.Equal(source, Assert.Single(range).Source);
        Assert.Equal(shouldReuse ? 0.92m : 0.94m, point);
        Assert.Equal(shouldReuse ? 0 : 1, inner.PointCalls);
    }

    private sealed class RangeAwareInner : ICurrencyExchangeService, ICurrencyExchangeRateResolutionService
    {
        public List<(DateTime Date, decimal? Value, CurrencyExchangeRateSource Source)> RangeResults { get; init; } = [];

        public Func<Currency, Currency, DateTime, CurrencyExchangeRateResult> PointResult { get; init; } =
            (_, _, _) => CurrencyExchangeRateResult.NotFound();

        public List<DateTime> PointRequests { get; } = [];

        public Task<List<(DateTime Date, decimal? Value)>> GetExchangeRateAsync(
            Currency fromCurrency,
            Currency toCurrency,
            DateTime dateStart,
            DateTime dateEnd) =>
            Task.FromResult(RangeResults.Select(rate => (rate.Date, rate.Value)).ToList());

        public Task<decimal?> GetExchangeRateAsync(Currency fromCurrency, Currency toCurrency, DateTime date)
        {
            PointRequests.Add(date);
            return Task.FromResult(PointResult(fromCurrency, toCurrency, date).Value);
        }

        public Task<CurrencyExchangeRateResult> GetExchangeRateResultAsync(
            Currency fromCurrency,
            Currency toCurrency,
            DateTime date)
        {
            PointRequests.Add(date);
            return Task.FromResult(PointResult(fromCurrency, toCurrency, date));
        }

        public async Task<CurrencyExchangeRateResolution> GetExchangeRateWithSourceAsync(
            Currency fromCurrency,
            Currency toCurrency,
            DateTime date) =>
            new(await GetExchangeRateResultAsync(fromCurrency, toCurrency, date), CurrencyExchangeRateSource.Provider);

        public Task<List<(DateTime Date, decimal? Value, CurrencyExchangeRateSource Source)>> GetExchangeRateRangeWithProvenanceAsync(
            Currency fromCurrency,
            Currency toCurrency,
            DateTime dateStart,
            DateTime dateEnd,
            IReadOnlyDictionary<DateTime, CurrencyExchangeRateResolution>? cachedRates = null) =>
            Task.FromResult(RangeResults);

        public int PointCalls => PointRequests.Count;

        public int PointCallsFor(DateTime date) => PointRequests.Count(requestedDate => requestedDate == date);
    }
}