using FinanceManager.Application.FinancialAccounts.Currencies.ExchangeRates;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Currencies.Repositories;
using FinanceManager.Domain.FinancialAccounts.Currencies.Services;
using Moq;

namespace FinanceManager.Tests.Unit.Application.Services;

[Trait("Category", "Unit")]
public class StoredCurrencyExchangeRateSourceTests
{
    private static readonly Currency _usd = new(1, "USD", "$");
    private static readonly Currency _eur = new(2, "EUR", "€");

    [Fact]
    public async Task ResolveAsync_OverlappingCalls_AreSerializedWithinTheScopedSource()
    {
        var repository = new Mock<IExchangeRateRepository>();
        var inner = new Mock<ICurrencyExchangeRateSource>();
        var firstInnerCallEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirstInnerCall = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var activeInnerCalls = 0;
        var maximumActiveInnerCalls = 0;

        repository
            .Setup(x => x.GetRange(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<(string From, string To, DateTime Date), decimal>());
        repository
            .Setup(x => x.Get(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((decimal?)null);
        repository
            .Setup(x => x.AddRange(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<IReadOnlyCollection<(DateTime Date, decimal Rate)>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
        repository
            .Setup(x => x.Add(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<DateTime>(),
                It.IsAny<decimal>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        inner
            .Setup(x => x.ResolveAsync(
                It.IsAny<Currency>(),
                It.IsAny<Currency>(),
                It.IsAny<IReadOnlyCollection<DateTime>>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<CurrencyExchangeRateResolutionContext>()))
            .Returns(async (Currency from, Currency to, IReadOnlyCollection<DateTime> dates, bool _, CancellationToken _, CurrencyExchangeRateResolutionContext? _) =>
            {
                var active = Interlocked.Increment(ref activeInnerCalls);
                Interlocked.Exchange(ref maximumActiveInnerCalls, Math.Max(maximumActiveInnerCalls, active));
                firstInnerCallEntered.TrySetResult();
                await releaseFirstInnerCall.Task;
                Interlocked.Decrement(ref activeInnerCalls);

                return dates.ToDictionary(
                    date => date,
                    date => new CurrencyExchangeRateResolution(
                        CurrencyExchangeRateResult.Success(0.92m),
                        CurrencyExchangeRateSource.Provider));
            });

        var sut = new StoredCurrencyExchangeRateSource(repository.Object, inner.Object);
        var cancellationToken = TestContext.Current.CancellationToken;
        var first = sut.ResolveAsync(
            _usd,
            _eur,
            [new DateTime(2024, 1, 15), new DateTime(2024, 1, 16)],
            false,
            cancellationToken);
        await firstInnerCallEntered.Task.WaitAsync(cancellationToken);

        var second = sut.ResolveAsync(_usd, _eur, [new DateTime(2024, 1, 17)], false, cancellationToken);
        releaseFirstInnerCall.TrySetResult();
        await Task.WhenAll(first, second);

        Assert.Equal(1, maximumActiveInnerCalls);
    }

    [Fact]
    public async Task ResolveAsync_PointMode_WhenFoundDirectly_UsesGetAndDoesNotCallInnerOrAdd()
    {
        var repository = new Mock<IExchangeRateRepository>();
        var inner = new Mock<ICurrencyExchangeRateSource>();
        var date = new DateTime(2024, 1, 15, 0, 0, 0, DateTimeKind.Utc);
        var ct = TestContext.Current.CancellationToken;

        repository
            .Setup(x => x.Get("USD", "EUR", date, ct))
            .ReturnsAsync(0.92m);

        var sut = new StoredCurrencyExchangeRateSource(repository.Object, inner.Object);
        var results = await sut.ResolveAsync(_usd, _eur, [date], reuseCachedMisses: true, ct);

        Assert.True(results.TryGetValue(date, out var resolution));
        Assert.Equal(CurrencyExchangeRateSource.Stored, resolution.Source);
        Assert.Equal(0.92m, resolution.Result.Value);

        repository.Verify(x => x.Get("USD", "EUR", date, ct), Times.Once);
        repository.Verify(x => x.Get("EUR", "USD", It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
        repository.Verify(x => x.GetRange(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
        inner.Verify(x => x.ResolveAsync(It.IsAny<Currency>(), It.IsAny<Currency>(), It.IsAny<IReadOnlyCollection<DateTime>>(), It.IsAny<bool>(), It.IsAny<CancellationToken>(), It.IsAny<CurrencyExchangeRateResolutionContext>()), Times.Never);
        repository.Verify(x => x.Add(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<decimal>(), It.IsAny<CancellationToken>()), Times.Never);
        repository.Verify(x => x.AddRange(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IReadOnlyCollection<(DateTime Date, decimal Rate)>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ResolveAsync_PointMode_WhenDirectMissesButInverseHits_UsesGetAndInvertsRate()
    {
        var repository = new Mock<IExchangeRateRepository>();
        var inner = new Mock<ICurrencyExchangeRateSource>();
        var date = new DateTime(2024, 1, 15, 0, 0, 0, DateTimeKind.Utc);
        var ct = TestContext.Current.CancellationToken;

        repository
            .Setup(x => x.Get("USD", "EUR", date, ct))
            .ReturnsAsync((decimal?)null);
        repository
            .Setup(x => x.Get("EUR", "USD", date, ct))
            .ReturnsAsync(2m);

        var sut = new StoredCurrencyExchangeRateSource(repository.Object, inner.Object);
        var results = await sut.ResolveAsync(_usd, _eur, [date], reuseCachedMisses: true, ct);

        Assert.True(results.TryGetValue(date, out var resolution));
        Assert.Equal(CurrencyExchangeRateSource.Stored, resolution.Source);
        Assert.Equal(0.5m, resolution.Result.Value);

        repository.Verify(x => x.Get("USD", "EUR", date, ct), Times.Once);
        repository.Verify(x => x.Get("EUR", "USD", date, ct), Times.Once);
        repository.Verify(x => x.GetRange(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
        inner.Verify(x => x.ResolveAsync(It.IsAny<Currency>(), It.IsAny<Currency>(), It.IsAny<IReadOnlyCollection<DateTime>>(), It.IsAny<bool>(), It.IsAny<CancellationToken>(), It.IsAny<CurrencyExchangeRateResolutionContext>()), Times.Never);
    }

    [Fact]
    public async Task ResolveAsync_PointMode_WhenMissingInDatabase_CallsInnerAndAdd()
    {
        var repository = new Mock<IExchangeRateRepository>();
        var inner = new Mock<ICurrencyExchangeRateSource>();
        var date = new DateTime(2024, 1, 15, 0, 0, 0, DateTimeKind.Utc);
        var ct = TestContext.Current.CancellationToken;

        repository
            .Setup(x => x.Get(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((decimal?)null);
        inner
            .Setup(x => x.ResolveAsync(_usd, _eur, It.Is<IReadOnlyCollection<DateTime>>(d => d.Count == 1 && d.Contains(date)), true, ct, It.IsAny<CurrencyExchangeRateResolutionContext>()))
            .ReturnsAsync(new Dictionary<DateTime, CurrencyExchangeRateResolution>
            {
                [date] = new(CurrencyExchangeRateResult.Success(0.92m), CurrencyExchangeRateSource.Provider)
            });

        var sut = new StoredCurrencyExchangeRateSource(repository.Object, inner.Object);
        var results = await sut.ResolveAsync(_usd, _eur, [date], reuseCachedMisses: true, ct);

        Assert.True(results.TryGetValue(date, out var resolution));
        Assert.Equal(CurrencyExchangeRateSource.Provider, resolution.Source);
        Assert.Equal(0.92m, resolution.Result.Value);

        repository.Verify(x => x.Add("USD", "EUR", date, 0.92m, ct), Times.Once);
        repository.Verify(x => x.AddRange(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IReadOnlyCollection<(DateTime Date, decimal Rate)>>(), It.IsAny<CancellationToken>()), Times.Never);
        repository.Verify(x => x.GetRange(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ResolveAsync_RangeMode_ZeroMissingDates_UsesGetRangeAndNoInnerOrPersistence()
    {
        var repository = new Mock<IExchangeRateRepository>();
        var inner = new Mock<ICurrencyExchangeRateSource>();
        var dateStart = new DateTime(2024, 1, 15, 0, 0, 0, DateTimeKind.Utc);
        var dateEnd = new DateTime(2024, 1, 17, 0, 0, 0, DateTimeKind.Utc);
        var ct = TestContext.Current.CancellationToken;

        repository
            .Setup(x => x.GetRange("USD", "EUR", dateStart, dateEnd, ct))
            .ReturnsAsync(new Dictionary<(string From, string To, DateTime Date), decimal>
            {
                { ("USD", "EUR", dateStart), 0.92m },
                { ("USD", "EUR", dateStart.AddDays(1)), 0.91m },
                { ("USD", "EUR", dateEnd), 0.93m }
            });

        var sut = new StoredCurrencyExchangeRateSource(repository.Object, inner.Object);
        var results = await sut.ResolveAsync(_usd, _eur, [dateStart, dateStart.AddDays(1), dateEnd], reuseCachedMisses: false, ct);

        Assert.Equal(3, results.Count);
        Assert.All(results.Values, r => Assert.Equal(CurrencyExchangeRateSource.Stored, r.Source));
        Assert.Equal(0.92m, results[dateStart].Result.Value);
        Assert.Equal(0.91m, results[dateStart.AddDays(1)].Result.Value);
        Assert.Equal(0.93m, results[dateEnd].Result.Value);

        repository.Verify(x => x.GetRange("USD", "EUR", dateStart, dateEnd, ct), Times.Once);
        repository.Verify(x => x.Get(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
        inner.Verify(x => x.ResolveAsync(It.IsAny<Currency>(), It.IsAny<Currency>(), It.IsAny<IReadOnlyCollection<DateTime>>(), It.IsAny<bool>(), It.IsAny<CancellationToken>(), It.IsAny<CurrencyExchangeRateResolutionContext>()), Times.Never);
        repository.Verify(x => x.AddRange(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IReadOnlyCollection<(DateTime Date, decimal Rate)>>(), It.IsAny<CancellationToken>()), Times.Never);
        repository.Verify(x => x.Add(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<decimal>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ResolveAsync_RangeMode_SingleDate_WhenFound_UsesGetRangeAndNeverCallsPointGet()
    {
        var repository = new Mock<IExchangeRateRepository>();
        var inner = new Mock<ICurrencyExchangeRateSource>();
        var date = new DateTime(2024, 1, 15, 0, 0, 0, DateTimeKind.Utc);
        var ct = TestContext.Current.CancellationToken;

        repository
            .Setup(x => x.GetRange("USD", "EUR", date, date, ct))
            .ReturnsAsync(new Dictionary<(string From, string To, DateTime Date), decimal>
            {
                { ("USD", "EUR", date), 0.92m }
            });

        var sut = new StoredCurrencyExchangeRateSource(repository.Object, inner.Object);
        var results = await sut.ResolveAsync(_usd, _eur, [date], reuseCachedMisses: false, ct);

        Assert.True(results.TryGetValue(date, out var resolution));
        Assert.Equal(CurrencyExchangeRateSource.Stored, resolution.Source);
        Assert.Equal(0.92m, resolution.Result.Value);

        repository.Verify(x => x.GetRange("USD", "EUR", date, date, ct), Times.Once);
        repository.Verify(x => x.Get(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
        inner.Verify(x => x.ResolveAsync(It.IsAny<Currency>(), It.IsAny<Currency>(), It.IsAny<IReadOnlyCollection<DateTime>>(), It.IsAny<bool>(), It.IsAny<CancellationToken>(), It.IsAny<CurrencyExchangeRateResolutionContext>()), Times.Never);
        repository.Verify(x => x.Add(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<decimal>(), It.IsAny<CancellationToken>()), Times.Never);
        repository.Verify(x => x.AddRange(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IReadOnlyCollection<(DateTime Date, decimal Rate)>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ResolveAsync_RangeMode_SingleDate_WhenMissing_UsesGetRangeAndPersistsViaAddRange()
    {
        var repository = new Mock<IExchangeRateRepository>();
        var inner = new Mock<ICurrencyExchangeRateSource>();
        var date = new DateTime(2024, 1, 15, 0, 0, 0, DateTimeKind.Utc);
        var ct = TestContext.Current.CancellationToken;

        repository
            .Setup(x => x.GetRange("USD", "EUR", date, date, ct))
            .ReturnsAsync(new Dictionary<(string From, string To, DateTime Date), decimal>());
        repository
            .Setup(x => x.AddRange("USD", "EUR", It.IsAny<IReadOnlyCollection<(DateTime Date, decimal Rate)>>(), ct))
            .ReturnsAsync(1);

        inner
            .Setup(x => x.ResolveAsync(_usd, _eur, It.Is<IReadOnlyCollection<DateTime>>(d => d.Count == 1 && d.Contains(date)), false, ct, It.IsAny<CurrencyExchangeRateResolutionContext>()))
            .ReturnsAsync(new Dictionary<DateTime, CurrencyExchangeRateResolution>
            {
                [date] = new(CurrencyExchangeRateResult.Success(0.92m), CurrencyExchangeRateSource.Provider)
            });

        var sut = new StoredCurrencyExchangeRateSource(repository.Object, inner.Object);
        var results = await sut.ResolveAsync(_usd, _eur, [date], reuseCachedMisses: false, ct);

        Assert.True(results.TryGetValue(date, out var resolution));
        Assert.Equal(CurrencyExchangeRateSource.Provider, resolution.Source);
        Assert.Equal(0.92m, resolution.Result.Value);

        repository.Verify(x => x.GetRange("USD", "EUR", date, date, ct), Times.Once);
        repository.Verify(x => x.Get(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
        repository.Verify(x => x.AddRange("USD", "EUR", It.Is<IReadOnlyCollection<(DateTime Date, decimal Rate)>>(r => r.Count == 1 && r.First().Date == date && r.First().Rate == 0.92m), ct), Times.Once);
        repository.Verify(x => x.Add(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<decimal>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ResolveAsync_RangeMode_ManyMissingDates_BatchesMissingToInnerAndAddRange()
    {
        var repository = new Mock<IExchangeRateRepository>();
        var inner = new Mock<ICurrencyExchangeRateSource>();
        var startDate = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var dates = Enumerable.Range(0, 60).Select(i => startDate.AddDays(i)).ToList();
        var endDate = dates[^1];
        var ct = TestContext.Current.CancellationToken;

        repository
            .Setup(x => x.GetRange("USD", "EUR", startDate, endDate, ct))
            .ReturnsAsync(new Dictionary<(string From, string To, DateTime Date), decimal>());
        repository
            .Setup(x => x.AddRange("USD", "EUR", It.IsAny<IReadOnlyCollection<(DateTime Date, decimal Rate)>>(), ct))
            .ReturnsAsync(60);

        inner
            .Setup(x => x.ResolveAsync(_usd, _eur, It.Is<IReadOnlyCollection<DateTime>>(d => d.Count == 60), false, ct, It.IsAny<CurrencyExchangeRateResolutionContext>()))
            .ReturnsAsync(dates.ToDictionary(
                d => d,
                d => new CurrencyExchangeRateResolution(CurrencyExchangeRateResult.Success(0.92m), CurrencyExchangeRateSource.Provider)));

        var sut = new StoredCurrencyExchangeRateSource(repository.Object, inner.Object);
        var results = await sut.ResolveAsync(_usd, _eur, dates, reuseCachedMisses: false, ct);

        Assert.Equal(60, results.Count);
        Assert.All(results.Values, r => Assert.Equal(CurrencyExchangeRateSource.Provider, r.Source));

        repository.Verify(x => x.GetRange("USD", "EUR", startDate, endDate, ct), Times.Once);
        repository.Verify(x => x.Get(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
        inner.Verify(x => x.ResolveAsync(_usd, _eur, It.Is<IReadOnlyCollection<DateTime>>(d => d.Count == 60), false, ct, It.IsAny<CurrencyExchangeRateResolutionContext>()), Times.Once);
        repository.Verify(x => x.AddRange("USD", "EUR", It.Is<IReadOnlyCollection<(DateTime Date, decimal Rate)>>(r => r.Count == 60), ct), Times.Once);
        repository.Verify(x => x.Add(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<decimal>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ResolveAsync_RangeMode_PartiallyStored_CallsGetRangeThenResolvesAndPersistsOnlyMissing()
    {
        var repository = new Mock<IExchangeRateRepository>();
        var inner = new Mock<ICurrencyExchangeRateSource>();
        var d1 = new DateTime(2024, 1, 15, 0, 0, 0, DateTimeKind.Utc);
        var d2 = new DateTime(2024, 1, 16, 0, 0, 0, DateTimeKind.Utc);
        var d3 = new DateTime(2024, 1, 17, 0, 0, 0, DateTimeKind.Utc);
        var ct = TestContext.Current.CancellationToken;

        repository
            .Setup(x => x.GetRange("USD", "EUR", d1, d3, ct))
            .ReturnsAsync(new Dictionary<(string From, string To, DateTime Date), decimal>
            {
                { ("USD", "EUR", d1), 0.92m }
            });
        repository
            .Setup(x => x.AddRange("USD", "EUR", It.IsAny<IReadOnlyCollection<(DateTime Date, decimal Rate)>>(), ct))
            .ReturnsAsync(2);

        inner
            .Setup(x => x.ResolveAsync(_usd, _eur, It.Is<IReadOnlyCollection<DateTime>>(d => d.Count == 2 && d.Contains(d2) && d.Contains(d3)), false, ct, It.IsAny<CurrencyExchangeRateResolutionContext>()))
            .ReturnsAsync(new Dictionary<DateTime, CurrencyExchangeRateResolution>
            {
                [d2] = new(CurrencyExchangeRateResult.Success(0.91m), CurrencyExchangeRateSource.Provider),
                [d3] = new(CurrencyExchangeRateResult.Success(0.93m), CurrencyExchangeRateSource.Provider)
            });

        var sut = new StoredCurrencyExchangeRateSource(repository.Object, inner.Object);
        var results = await sut.ResolveAsync(_usd, _eur, [d1, d2, d3], reuseCachedMisses: false, ct);

        Assert.Equal(3, results.Count);
        Assert.Equal(CurrencyExchangeRateSource.Stored, results[d1].Source);
        Assert.Equal(CurrencyExchangeRateSource.Provider, results[d2].Source);
        Assert.Equal(CurrencyExchangeRateSource.Provider, results[d3].Source);

        repository.Verify(x => x.GetRange("USD", "EUR", d1, d3, ct), Times.Once);
        repository.Verify(x => x.Get(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
        inner.Verify(x => x.ResolveAsync(_usd, _eur, It.Is<IReadOnlyCollection<DateTime>>(d => d.Count == 2 && d.Contains(d2) && d.Contains(d3)), false, ct, It.IsAny<CurrencyExchangeRateResolutionContext>()), Times.Once);
        repository.Verify(x => x.AddRange("USD", "EUR", It.Is<IReadOnlyCollection<(DateTime Date, decimal Rate)>>(r => r.Count == 2), ct), Times.Once);
        repository.Verify(x => x.Add(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<decimal>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ResolveAsync_RangeMode_WhenProviderReturnsUnavailable_DoesNotPersistFailures()
    {
        var repository = new Mock<IExchangeRateRepository>();
        var inner = new Mock<ICurrencyExchangeRateSource>();
        var d1 = new DateTime(2024, 1, 15, 0, 0, 0, DateTimeKind.Utc);
        var d2 = new DateTime(2024, 1, 16, 0, 0, 0, DateTimeKind.Utc);
        var ct = TestContext.Current.CancellationToken;

        repository
            .Setup(x => x.GetRange("USD", "EUR", d1, d2, ct))
            .ReturnsAsync(new Dictionary<(string From, string To, DateTime Date), decimal>());

        inner
            .Setup(x => x.ResolveAsync(_usd, _eur, It.IsAny<IReadOnlyCollection<DateTime>>(), false, ct, It.IsAny<CurrencyExchangeRateResolutionContext>()))
            .ReturnsAsync(new Dictionary<DateTime, CurrencyExchangeRateResolution>
            {
                [d1] = new(CurrencyExchangeRateResult.NotFound(), CurrencyExchangeRateSource.Unavailable),
                [d2] = new(CurrencyExchangeRateResult.Failed(), CurrencyExchangeRateSource.Unavailable)
            });

        var sut = new StoredCurrencyExchangeRateSource(repository.Object, inner.Object);
        var results = await sut.ResolveAsync(_usd, _eur, [d1, d2], reuseCachedMisses: false, ct);

        Assert.Equal(2, results.Count);
        Assert.False(results[d1].Result.IsSuccess);
        Assert.False(results[d2].Result.IsSuccess);

        repository.Verify(x => x.AddRange(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IReadOnlyCollection<(DateTime Date, decimal Rate)>>(), It.IsAny<CancellationToken>()), Times.Never);
        repository.Verify(x => x.Add(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<decimal>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ResolveAsync_PropagatesCancellationToken_ToAllCalls()
    {
        var repository = new Mock<IExchangeRateRepository>();
        var inner = new Mock<ICurrencyExchangeRateSource>();
        using var cts = new CancellationTokenSource();
        var ct = cts.Token;
        var date = new DateTime(2024, 1, 15, 0, 0, 0, DateTimeKind.Utc);

        repository
            .Setup(x => x.GetRange("USD", "EUR", date, date, ct))
            .ReturnsAsync(new Dictionary<(string From, string To, DateTime Date), decimal>());
        repository
            .Setup(x => x.AddRange("USD", "EUR", It.IsAny<IReadOnlyCollection<(DateTime Date, decimal Rate)>>(), ct))
            .ReturnsAsync(1);

        inner
            .Setup(x => x.ResolveAsync(_usd, _eur, It.IsAny<IReadOnlyCollection<DateTime>>(), false, ct, It.IsAny<CurrencyExchangeRateResolutionContext>()))
            .ReturnsAsync(new Dictionary<DateTime, CurrencyExchangeRateResolution>
            {
                [date] = new(CurrencyExchangeRateResult.Success(0.92m), CurrencyExchangeRateSource.Provider)
            });

        var sut = new StoredCurrencyExchangeRateSource(repository.Object, inner.Object);
        await sut.ResolveAsync(_usd, _eur, [date], reuseCachedMisses: false, ct);

        repository.Verify(x => x.GetRange("USD", "EUR", date, date, ct), Times.Once);
        inner.Verify(x => x.ResolveAsync(_usd, _eur, It.IsAny<IReadOnlyCollection<DateTime>>(), false, ct, It.IsAny<CurrencyExchangeRateResolutionContext>()), Times.Once);
        repository.Verify(x => x.AddRange("USD", "EUR", It.IsAny<IReadOnlyCollection<(DateTime Date, decimal Rate)>>(), ct), Times.Once);
    }

    [Fact]
    public async Task ResolveAsync_RangeMode_UsdCrossLeg_SingleDateMissing_UsesGetRangeAndPersistsViaAddRange()
    {
        var repository = new Mock<IExchangeRateRepository>();
        var inner = new Mock<ICurrencyExchangeRateSource>();
        var gbp = new Currency(3, "GBP", "£");
        var date = new DateTime(2024, 1, 15, 0, 0, 0, DateTimeKind.Utc);
        var ct = TestContext.Current.CancellationToken;

        repository
            .Setup(x => x.GetRange("GBP", "USD", date, date, ct))
            .ReturnsAsync(new Dictionary<(string From, string To, DateTime Date), decimal>());
        repository
            .Setup(x => x.AddRange("GBP", "USD", It.IsAny<IReadOnlyCollection<(DateTime Date, decimal Rate)>>(), ct))
            .ReturnsAsync(1);

        inner
            .Setup(x => x.ResolveAsync(gbp, _usd, It.Is<IReadOnlyCollection<DateTime>>(d => d.Count == 1 && d.Contains(date)), false, ct, It.IsAny<CurrencyExchangeRateResolutionContext>()))
            .ReturnsAsync(new Dictionary<DateTime, CurrencyExchangeRateResolution>
            {
                [date] = new(CurrencyExchangeRateResult.Success(1.25m), CurrencyExchangeRateSource.Provider)
            });

        var sut = new StoredCurrencyExchangeRateSource(repository.Object, inner.Object);
        var results = await sut.ResolveAsync(gbp, _usd, [date], reuseCachedMisses: false, ct);

        Assert.True(results.TryGetValue(date, out var resolution));
        Assert.Equal(CurrencyExchangeRateSource.Provider, resolution.Source);
        Assert.Equal(1.25m, resolution.Result.Value);

        repository.Verify(x => x.GetRange("GBP", "USD", date, date, ct), Times.Once);
        repository.Verify(x => x.Get(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
        repository.Verify(x => x.AddRange("GBP", "USD", It.Is<IReadOnlyCollection<(DateTime Date, decimal Rate)>>(r => r.Count == 1 && r.First().Date == date && r.First().Rate == 1.25m), ct), Times.Once);
        repository.Verify(x => x.Add(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<decimal>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CurrencyExchangeService_RangeMode_SingleDateUsdCross_UsesBatchedDatabaseMethodsForBothLegs()
    {
        var repository = new Mock<IExchangeRateRepository>();
        var inner = new Mock<ICurrencyExchangeRateSource>();
        var gbp = new Currency(3, "GBP", "£");
        var pln = new Currency(4, "PLN", "zł");
        var date = new DateTime(2024, 1, 15, 0, 0, 0, DateTimeKind.Utc);

        // GBP -> PLN direct range misses in DB
        repository
            .Setup(x => x.GetRange("GBP", "PLN", date, date, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<(string From, string To, DateTime Date), decimal>());

        // GBP -> USD leg misses in DB
        repository
            .Setup(x => x.GetRange("GBP", "USD", date, date, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<(string From, string To, DateTime Date), decimal>());

        // USD -> PLN leg misses in DB
        repository
            .Setup(x => x.GetRange("USD", "PLN", date, date, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<(string From, string To, DateTime Date), decimal>());

        // Direct GBP -> PLN provider returns unavailable
        inner
            .Setup(x => x.ResolveAsync(gbp, pln, It.IsAny<IReadOnlyCollection<DateTime>>(), false, It.IsAny<CancellationToken>(), It.IsAny<CurrencyExchangeRateResolutionContext>()))
            .ReturnsAsync(new Dictionary<DateTime, CurrencyExchangeRateResolution>
            {
                [date] = new(CurrencyExchangeRateResult.NotFound(), CurrencyExchangeRateSource.Unavailable)
            });

        // GBP -> USD provider succeeds
        inner
            .Setup(x => x.ResolveAsync(gbp, DefaultCurrency.USD, It.IsAny<IReadOnlyCollection<DateTime>>(), false, It.IsAny<CancellationToken>(), It.IsAny<CurrencyExchangeRateResolutionContext>()))
            .ReturnsAsync(new Dictionary<DateTime, CurrencyExchangeRateResolution>
            {
                [date] = new(CurrencyExchangeRateResult.Success(1.25m), CurrencyExchangeRateSource.Provider)
            });

        // USD -> PLN provider succeeds
        inner
            .Setup(x => x.ResolveAsync(DefaultCurrency.USD, pln, It.IsAny<IReadOnlyCollection<DateTime>>(), false, It.IsAny<CancellationToken>(), It.IsAny<CurrencyExchangeRateResolutionContext>()))
            .ReturnsAsync(new Dictionary<DateTime, CurrencyExchangeRateResolution>
            {
                [date] = new(CurrencyExchangeRateResult.Success(4m), CurrencyExchangeRateSource.Provider)
            });

        var source = new StoredCurrencyExchangeRateSource(repository.Object, inner.Object);
        var service = new CurrencyExchangeService(source);

        var results = await service.GetExchangeRateRangeWithProvenanceAsync(gbp, pln, date, date);

        var item = Assert.Single(results);
        Assert.Equal(5m, item.Value);
        Assert.Equal(CurrencyExchangeRateSource.DerivedViaUsd, item.Source);

        // All DB reads in range mode used GetRange, never Get
        repository.Verify(x => x.Get(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
        repository.Verify(x => x.GetRange("GBP", "PLN", date, date, It.IsAny<CancellationToken>()), Times.Once);
        repository.Verify(x => x.GetRange("GBP", "USD", date, date, It.IsAny<CancellationToken>()), Times.Once);
        repository.Verify(x => x.GetRange("USD", "PLN", date, date, It.IsAny<CancellationToken>()), Times.Once);

        // All persistence in range mode used AddRange, never Add
        repository.Verify(x => x.Add(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<decimal>(), It.IsAny<CancellationToken>()), Times.Never);
        repository.Verify(x => x.AddRange("GBP", "USD", It.Is<IReadOnlyCollection<(DateTime Date, decimal Rate)>>(r => r.Count == 1 && r.First().Rate == 1.25m), It.IsAny<CancellationToken>()), Times.Once);
        repository.Verify(x => x.AddRange("USD", "PLN", It.Is<IReadOnlyCollection<(DateTime Date, decimal Rate)>>(r => r.Count == 1 && r.First().Rate == 4m), It.IsAny<CancellationToken>()), Times.Once);
    }
}