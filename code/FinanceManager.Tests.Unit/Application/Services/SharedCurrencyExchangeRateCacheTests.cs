using FinanceManager.Application.FinancialAccounts.Currencies.ExchangeRates;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Currencies.Repositories;
using FinanceManager.Domain.FinancialAccounts.Currencies.Services;
using Microsoft.Extensions.Caching.Memory;
using Moq;

namespace FinanceManager.Tests.Unit.Application.Services;

[Trait("Category", "Unit")]
public class SharedCurrencyExchangeRateCacheTests : IDisposable
{
    private static readonly Currency _usd = DefaultCurrency.USD;
    private static readonly Currency _eur = new(2, "EUR", "€");
    private readonly MemoryCache _cache = new(new MemoryCacheOptions());
    private readonly Mock<IExchangeRateRepository> _repository = new();
    private readonly Mock<ICurrencyExchangeRateProvider> _provider = new();

    public SharedCurrencyExchangeRateCacheTests()
    {
        _repository.Setup(x => x.GetRange(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), default))
            .ReturnsAsync(new Dictionary<(string From, string To, DateTime Date), decimal>());
        _provider.Setup(x => x.GetExchangeRateAsync(
            It.IsAny<Currency>(), It.IsAny<Currency>(), It.IsAny<DateTime>()))
            .ReturnsAsync(new CurrencyExchangeRateProviderResult(CurrencyExchangeRateProviderStatus.Success, 0.92m));
        _provider.Setup(x => x.GetExchangeRateAsync(
            It.IsAny<Currency>(), It.IsAny<Currency>(), It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync((Currency from, Currency to, DateTime start, DateTime end) =>
                Enumerable.Range(0, (end - start).Days + 1)
                    .Select(i => (start.AddDays(i),
                        new CurrencyExchangeRateProviderResult(CurrencyExchangeRateProviderStatus.Success, 0.94m)))
                    .ToList());
    }

    public void Dispose() => _cache.Dispose();

    private CachedCurrencyExchangeService CreateSut() =>
        new(new CurrencyExchangeService(_repository.Object, [_provider.Object]), _cache);

    [Fact]
    public async Task StoredPointResults_SatisfyRangeWithoutRepositoryOrProviderReads()
    {
        var start = new DateTime(2024, 1, 15);
        _repository.Setup(x => x.Get("USD", "EUR", It.IsAny<DateTime>(), default)).ReturnsAsync(0.91m);
        var sut = CreateSut();
        await sut.GetExchangeRateAsync(_usd, _eur, start);
        await sut.GetExchangeRateAsync(_usd, _eur, start.AddDays(1));
        _repository.Invocations.Clear();

        var range = await sut.GetExchangeRateRangeWithProvenanceAsync(_usd, _eur, start, start.AddDays(1));

        Assert.Equal(2, range.Count);
        Assert.All(range, rate =>
        {
            Assert.Equal(0.91m, rate.Value);
            Assert.Equal(CurrencyExchangeRateSource.Stored, rate.Source);
        });
        _repository.VerifyNoOtherCalls();
        _provider.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task InverseStoredPointResult_CanSatisfyRange()
    {
        var date = new DateTime(2024, 1, 15);
        _repository.Setup(x => x.Get("EUR", "USD", date, default)).ReturnsAsync(2m);
        var sut = CreateSut();
        Assert.Equal(0.5m, await sut.GetExchangeRateAsync(_usd, _eur, date));
        _repository.Invocations.Clear();

        var range = await sut.GetExchangeRateRangeWithProvenanceAsync(_usd, _eur, date, date);

        Assert.Equal(0.5m, Assert.Single(range).Value);
        Assert.Equal(CurrencyExchangeRateSource.Stored, range[0].Source);
        _repository.VerifyNoOtherCalls();
        _provider.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ProviderPointResults_SatisfyReversedRangeWithNormalizedCurrencyAndDates()
    {
        var start = new DateTime(2024, 1, 15);
        var sut = CreateSut();
        await sut.GetExchangeRateAsync(_usd, _eur, start.AddHours(12));
        await sut.GetExchangeRateAsync(_usd, _eur, start.AddDays(1));
        _repository.Invocations.Clear();
        _provider.Invocations.Clear();

        var range = await sut.GetExchangeRateRangeWithProvenanceAsync(
            new Currency(_usd.Id, " usd ", "$"), new Currency(_eur.Id, "eur", "€"),
            start.AddDays(1).AddHours(19), start.AddHours(3));

        Assert.Equal([start, start.AddDays(1)], range.Select(rate => rate.Date));
        Assert.All(range, rate => Assert.Equal(CurrencyExchangeRateSource.Provider, rate.Source));
        _repository.VerifyNoOtherCalls();
        _provider.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task OverlappingRanges_ResolveOnlyNewTailAndThenServeFullyCachedRange()
    {
        var start = new DateTime(2024, 1, 15);
        var sut = CreateSut();
        await sut.GetExchangeRateAsync(_usd, _eur, start, start.AddDays(2));
        _repository.Invocations.Clear();
        _provider.Invocations.Clear();

        var range = await sut.GetExchangeRateAsync(_usd, _eur, start.AddDays(1), start.AddDays(4));

        Assert.Equal(4, range.Count);
        _repository.Verify(x => x.GetRange("USD", "EUR", start.AddDays(3), start.AddDays(4), default), Times.Once);
        _repository.Verify(x => x.AddRange("USD", "EUR",
            It.Is<IReadOnlyCollection<(DateTime Date, decimal Rate)>>(rates => rates.Count == 2), default), Times.Once);
        _provider.Verify(x => x.GetExchangeRateAsync(_usd, _eur, start.AddDays(3), start.AddDays(4)), Times.Once);
        _repository.VerifyNoOtherCalls();
        _provider.VerifyNoOtherCalls();

        _repository.Invocations.Clear();
        _provider.Invocations.Clear();
        Assert.Equal(range, await sut.GetExchangeRateAsync(_usd, _eur, start.AddDays(1), start.AddDays(4)));
        Assert.Equal(0.94m, await sut.GetExchangeRateAsync(_usd, _eur, start.AddDays(4)));
        _repository.VerifyNoOtherCalls();
        _provider.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task SparseCacheHits_PreserveSingleProviderBudgetAndCarryForwardFromCachedDates()
    {
        var start = new DateTime(2024, 1, 1);
        var sut = CreateSut();
        for (var i = 0; i < 125; i += 2)
            await sut.GetExchangeRateAsync(_usd, _eur, start.AddDays(i));
        _repository.Invocations.Clear();
        _provider.Invocations.Clear();

        var range = await sut.GetExchangeRateRangeWithProvenanceAsync(_usd, _eur, start, start.AddDays(124));

        Assert.Equal(125, range.Count);
        Assert.Equal(60, range.Count(rate => rate.Value == 0.94m));
        foreach (var i in new[] { 121, 123 })
        {
            Assert.Equal(0.92m, range[i].Value);
            Assert.Equal(CurrencyExchangeRateSource.CarriedForward, range[i].Source);
        }
        _provider.Verify(x => x.GetExchangeRateAsync(_usd, _eur, start.AddDays(1), start.AddDays(119)), Times.Once);
        _provider.VerifyNoOtherCalls();
        _repository.Verify(x => x.GetRange("USD", "EUR", start.AddDays(1), start.AddDays(123), default), Times.Once);
        _repository.Verify(x => x.AddRange("USD", "EUR",
            It.Is<IReadOnlyCollection<(DateTime Date, decimal Rate)>>(rates => rates.Count == 60), default), Times.Once);
        _repository.VerifyNoOtherCalls();

        // Placeholders remain misses on the next request, so backfill continues.
        _provider.Invocations.Clear();
        var refreshed = await sut.GetExchangeRateRangeWithProvenanceAsync(_usd, _eur, start, start.AddDays(124));
        Assert.DoesNotContain(refreshed, rate => rate.Source == CurrencyExchangeRateSource.CarriedForward);
        _provider.Verify(x => x.GetExchangeRateAsync(_usd, _eur, start.AddDays(121), start.AddDays(123)), Times.Once);
        _provider.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task DerivedPointResult_DoesNotPreventRangeFromRetryingDirectPair()
    {
        var date = new DateTime(2024, 1, 15);
        var gbp = new Currency(3, "GBP", "£");
        _provider.Setup(x => x.GetExchangeRateAsync(gbp, _eur, date))
            .ReturnsAsync(new CurrencyExchangeRateProviderResult(CurrencyExchangeRateProviderStatus.NotFound));
        var sut = CreateSut();

        var point = await sut.GetExchangeRateWithSourceAsync(gbp, _eur, date);
        Assert.Equal(CurrencyExchangeRateSource.DerivedViaUsd, point.Source);
        var range = await sut.GetExchangeRateRangeWithProvenanceAsync(gbp, _eur, date, date);

        Assert.Equal(CurrencyExchangeRateSource.Provider, Assert.Single(range).Source);
        Assert.Equal(0.94m, await sut.GetExchangeRateAsync(gbp, _eur, date));
        _provider.Verify(x => x.GetExchangeRateAsync(gbp, _eur, date, date), Times.Once);
    }

    [Fact]
    public async Task CachedMiss_DoesNotHideNewlyPublishedRangeRate()
    {
        var date = DateTime.UtcNow.Date;
        _provider.Setup(x => x.GetExchangeRateAsync(_usd, _eur, date))
            .ReturnsAsync(new CurrencyExchangeRateProviderResult(CurrencyExchangeRateProviderStatus.NotYetPublished));
        var sut = CreateSut();
        Assert.Equal(CurrencyExchangeRateStatus.NotYetPublished,
            (await sut.GetExchangeRateResultAsync(_usd, _eur, date)).Status);

        var range = await sut.GetExchangeRateAsync(_usd, _eur, date, date);

        Assert.Equal(0.94m, Assert.Single(range).Value);
        Assert.Equal(0.94m, await sut.GetExchangeRateAsync(_usd, _eur, date));
    }

    [Fact]
    public async Task EvictedPointEntry_IsResolvedAgainByRange()
    {
        var date = new DateTime(2024, 1, 15);
        var sut = CreateSut();
        await sut.GetExchangeRateAsync(_usd, _eur, date);
        _cache.Compact(1);

        var range = await sut.GetExchangeRateAsync(_usd, _eur, date, date);

        Assert.Equal(0.94m, Assert.Single(range).Value);
        _provider.Verify(x => x.GetExchangeRateAsync(_usd, _eur, date, date), Times.Once);
    }

    [Fact]
    public async Task SameCurrencyPointAndRange_DoNotReadRepositoryOrProvider()
    {
        var date = new DateTime(2024, 1, 15);
        var sut = CreateSut();

        Assert.Equal(1m, await sut.GetExchangeRateAsync(_usd, _usd, date));
        var range = await sut.GetExchangeRateRangeWithProvenanceAsync(_usd, _usd, date, date);

        Assert.Equal(CurrencyExchangeRateSource.SameCurrency, Assert.Single(range).Source);
        Assert.Equal(1m, range[0].Value);
        _repository.VerifyNoOtherCalls();
        _provider.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task InvalidOrFutureRanges_DoNotReadRepositoryOrProvider()
    {
        var today = DateTime.UtcNow.Date;
        var sut = CreateSut();

        Assert.Empty(await sut.GetExchangeRateAsync(_usd, _eur, default, today));
        Assert.Empty(await sut.GetExchangeRateAsync(_usd, _eur, today.AddDays(1), today.AddDays(2)));
        await sut.GetExchangeRateAsync(_usd, _eur, today);
        _repository.Invocations.Clear();
        _provider.Invocations.Clear();
        Assert.Single(await sut.GetExchangeRateAsync(_usd, _eur, today, today.AddDays(2)));
        _repository.VerifyNoOtherCalls();
        _provider.VerifyNoOtherCalls();
    }
}