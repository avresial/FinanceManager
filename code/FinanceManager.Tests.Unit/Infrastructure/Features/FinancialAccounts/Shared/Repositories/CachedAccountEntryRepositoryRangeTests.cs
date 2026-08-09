using FinanceManager.Application.Dashboard;
using FinanceManager.Domain.Dashboard.Services;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Shared.Repositories;
using FinanceManager.Infrastructure.Features.FinancialAccounts.Shared.Repositories;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace FinanceManager.Tests.Unit.Infrastructure.Features.FinancialAccounts.Shared.Repositories;

/// <summary>
/// Tests for the range/bucket caching layer added in issue #456 on top of the
/// point-read cache from issue #455. All tests use a real in-process HybridCache
/// and a Moq inner repository so cache hits/misses are observable via call counts.
/// </summary>
[Trait("Category", "Unit")]
public class CachedAccountEntryRepositoryRangeTests
{
    private const int _accountId = 7;
    private const int _userId = 42;

    // Fixed months used in bucket-boundary tests; stay well inside the 12-month horizon.
    private static readonly DateTime _jan = new(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime _feb = new(2025, 2, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime _mar = new(2025, 3, 1, 0, 0, 0, DateTimeKind.Utc);

    private static HybridCache BuildCache()
    {
        var services = new ServiceCollection();
        services.AddHybridCache();
        return services.BuildServiceProvider().GetRequiredService<HybridCache>();
    }

    private static IAccountUserResolver ResolverFor(int accountId, int? userId)
    {
        var m = new Mock<IAccountUserResolver>();
        m.Setup(r => r.GetUserId(accountId, It.IsAny<CancellationToken>())).ReturnsAsync(userId);
        return m.Object;
    }

    private static CurrencyAccountEntry Entry(int entryId, DateTime date, decimal value = 100m) =>
        new(_accountId, entryId, date, value, value);

    // Long TTLs prevent expiry during tests; a large horizon (120 months) keeps the fixed 2025 test
    // dates always inside the bucket-cache path regardless of when the tests are run.
    private static readonly EntryRangeCacheOptions _defaultOptions = new()
    {
        HorizonMonths = 120,
        OpenMonthTtl = TimeSpan.FromHours(1),
        ElapsedMonthTtl = TimeSpan.FromHours(2)
    };

    private static readonly EntryRangeCacheOptions _twelveMonthOptions = new()
    {
        HorizonMonths = 12,
        OpenMonthTtl = TimeSpan.FromHours(1),
        ElapsedMonthTtl = TimeSpan.FromHours(2)
    };

    private static CachedAccountEntryRepository<CurrencyAccountEntry> CreateSut(
        IAccountEntryRepository<CurrencyAccountEntry> inner,
        HybridCache cache,
        EntryRangeCacheOptions? options = null,
        IAccountUserResolver? resolver = null,
        ICacheInvalidator? invalidator = null) =>
        new(inner,
            resolver ?? ResolverFor(_accountId, _userId),
            invalidator ?? Mock.Of<ICacheInvalidator>(),
            cache,
            options ?? _defaultOptions);

    // ── MonthBucket math ─────────────────────────────────────────────────────────

    [Fact]
    public void MonthBucket_MonthStart_TruncatesToFirstOfMonth()
    {
        var result = MonthBucket.MonthStart(new DateTime(2025, 3, 15, 12, 30, 0, DateTimeKind.Utc));
        Assert.Equal(new DateTime(2025, 3, 1, 0, 0, 0, DateTimeKind.Utc), result);
    }

    [Fact]
    public void MonthBucket_MonthEndInclusive_IsLastTickOfMonth()
    {
        var jan = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var result = MonthBucket.MonthEndInclusive(jan);
        // _jan 31 23:59:59.9999999 UTC
        var expected = new DateTime(2025, 2, 1, 0, 0, 0, DateTimeKind.Utc).AddTicks(-1);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void MonthBucket_TouchedMonths_ReturnsSingleMonthForIntraMonthRange()
    {
        var months = MonthBucket.TouchedMonths(
            new DateTime(2025, 3, 5, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2025, 3, 20, 0, 0, 0, DateTimeKind.Utc)).ToList();

        Assert.Single(months);
        Assert.Equal(_mar, months[0]);
    }

    [Fact]
    public void MonthBucket_TouchedMonths_ReturnsAllMonthsInRange()
    {
        var months = MonthBucket.TouchedMonths(_jan, _mar).ToList();
        Assert.Equal([_jan, _feb, _mar], months);
    }

    [Fact]
    public void MonthBucket_HorizonStart_IsFirstDayOfMonthNMonthsAgo()
    {
        var now = new DateTime(2025, 6, 15, 0, 0, 0, DateTimeKind.Utc);
        var horizon = MonthBucket.HorizonStart(12, now);
        Assert.Equal(new DateTime(2024, 6, 1, 0, 0, 0, DateTimeKind.Utc), horizon);
    }

    // ── Inflate-on-miss: partial request seeds the whole bucket ──────────────────

    [Fact]
    public async Task Get_PartialMonthRequest_InflatesInnerCallToFullMonth()
    {
        var jan10 = _jan.AddDays(9);
        var jan20 = _jan.AddDays(19);
        var janEnd = MonthBucket.MonthEndInclusive(_jan);

        var inner = new Mock<IAccountEntryRepository<CurrencyAccountEntry>>();
        inner.Setup(r => r.Get(_accountId, _jan, janEnd, It.IsAny<CancellationToken>()))
             .Returns(new[] { Entry(2, jan20), Entry(1, jan10) }.ToAsyncEnumerable());

        var sut = CreateSut(inner.Object, BuildCache());
        var ct = TestContext.Current.CancellationToken;

        var result = await sut.Get(_accountId, jan10, jan20, ct).ToListAsync(ct);

        // Inner was called with FULL month bounds, not the requested slice.
        inner.Verify(r => r.Get(_accountId, _jan, janEnd, It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task Get_SecondPartialMonthRequest_HitsCache_InnerCalledOnce()
    {
        // First call _jan 10–20 seeds the whole _january bucket.
        // Second call _jan 5–25 is a full cache hit — inner NOT called again.
        var jan5 = _jan.AddDays(4);
        var jan10 = _jan.AddDays(9);
        var jan20 = _jan.AddDays(19);
        var jan25 = _jan.AddDays(24);
        var janEnd = MonthBucket.MonthEndInclusive(_jan);
        var ct = TestContext.Current.CancellationToken;

        var inner = new Mock<IAccountEntryRepository<CurrencyAccountEntry>>();
        inner.Setup(r => r.Get(_accountId, _jan, janEnd, It.IsAny<CancellationToken>()))
             .Returns(new[]
             {
                 Entry(4, jan25), Entry(2, jan20), Entry(1, jan10), Entry(3, jan5)
             }.ToAsyncEnumerable());

        var cache = BuildCache();
        var sut = CreateSut(inner.Object, cache);

        var first = await sut.Get(_accountId, jan10, jan20, ct).ToListAsync(ct);
        var second = await sut.Get(_accountId, jan5, jan25, ct).ToListAsync(ct);

        inner.Verify(r => r.Get(_accountId, _jan, janEnd, It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal(2, first.Count);
        Assert.Equal(4, second.Count);
    }

    // ── Compose across buckets ────────────────────────────────────────────────────

    [Fact]
    public async Task Get_SpanningTwoMonths_ComposesAndSortsNewestFirst()
    {
        var jan15 = _jan.AddDays(14);
        var feb10 = _feb.AddDays(9);
        var janEnd = MonthBucket.MonthEndInclusive(_jan);
        var febEnd = MonthBucket.MonthEndInclusive(_feb);
        var ct = TestContext.Current.CancellationToken;

        var inner = new Mock<IAccountEntryRepository<CurrencyAccountEntry>>();
        inner.Setup(r => r.Get(_accountId, _jan, janEnd, It.IsAny<CancellationToken>()))
             .Returns(new[] { Entry(1, jan15) }.ToAsyncEnumerable());
        inner.Setup(r => r.Get(_accountId, _feb, febEnd, It.IsAny<CancellationToken>()))
             .Returns(new[] { Entry(2, feb10) }.ToAsyncEnumerable());

        var sut = CreateSut(inner.Object, BuildCache());
        var result = await sut.Get(_accountId, jan15, feb10, ct).ToListAsync(ct);

        Assert.Equal(2, result.Count);
        // Newest-first: _feb entry (id=2) before _jan entry (id=1)
        Assert.Equal(2, result[0].EntryId);
        Assert.Equal(1, result[1].EntryId);
    }

    [Fact]
    public async Task Get_OverlappingRequests_SecondFetchOnlyMissingMonth()
    {
        // First request seeds _jan + _feb; second request needs _feb + _mar → only _march hits DB.
        var jan15 = _jan.AddDays(14);
        var mar20 = _mar.AddDays(19);
        var janEnd = MonthBucket.MonthEndInclusive(_jan);
        var febEnd = MonthBucket.MonthEndInclusive(_feb);
        var marEnd = MonthBucket.MonthEndInclusive(_mar);
        var ct = TestContext.Current.CancellationToken;

        var inner = new Mock<IAccountEntryRepository<CurrencyAccountEntry>>();
        inner.Setup(r => r.Get(_accountId, _jan, janEnd, It.IsAny<CancellationToken>()))
             .Returns(new[] { Entry(1, jan15) }.ToAsyncEnumerable());
        inner.Setup(r => r.Get(_accountId, _feb, febEnd, It.IsAny<CancellationToken>()))
             .Returns(new[] { Entry(2, _feb.AddDays(10)) }.ToAsyncEnumerable());
        inner.Setup(r => r.Get(_accountId, _mar, marEnd, It.IsAny<CancellationToken>()))
             .Returns(new[] { Entry(3, _mar.AddDays(5)) }.ToAsyncEnumerable());

        var cache = BuildCache();
        var sut = CreateSut(inner.Object, cache);

        await sut.Get(_accountId, jan15, _feb.AddDays(28), ct).ToListAsync(ct);   // seeds _jan + _feb
        var result = await sut.Get(_accountId, _feb, mar20, ct).ToListAsync(ct);  // _feb=hit, _mar=miss

        inner.Verify(r => r.Get(_accountId, _jan, janEnd, It.IsAny<CancellationToken>()), Times.Once);
        inner.Verify(r => r.Get(_accountId, _feb, febEnd, It.IsAny<CancellationToken>()), Times.Once);
        inner.Verify(r => r.Get(_accountId, _mar, marEnd, It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal(2, result.Count);
    }

    // ── Empty months cached as empty (no re-query) ────────────────────────────────

    [Fact]
    public async Task Get_EmptyMonth_CachedAsEmpty_InnerNotCalledAgain()
    {
        var janEnd = MonthBucket.MonthEndInclusive(_jan);
        var ct = TestContext.Current.CancellationToken;

        var inner = new Mock<IAccountEntryRepository<CurrencyAccountEntry>>();
        inner.Setup(r => r.Get(_accountId, _jan, janEnd, It.IsAny<CancellationToken>()))
             .Returns(Array.Empty<CurrencyAccountEntry>().ToAsyncEnumerable());

        var cache = BuildCache();
        var sut = CreateSut(inner.Object, cache);

        var first = await sut.Get(_accountId, _jan, janEnd, ct).ToListAsync(ct);
        var second = await sut.Get(_accountId, _jan, janEnd, ct).ToListAsync(ct);

        inner.Verify(r => r.Get(_accountId, _jan, janEnd, It.IsAny<CancellationToken>()), Times.Once);
        Assert.Empty(first);
        Assert.Empty(second);
    }

    // ── Half-open boundary: entry on the 1st is NOT duplicated ───────────────────

    [Fact]
    public async Task Get_EntryOnFirstOfMonth_NotDuplicated_WhenBothMonthsQueried()
    {
        // An entry dated _feb 1 lives in the _february bucket.
        // Requesting _jan through _feb must return it exactly once.
        var feb1Entry = Entry(10, _feb);
        var janEnd = MonthBucket.MonthEndInclusive(_jan);
        var febEnd = MonthBucket.MonthEndInclusive(_feb);
        var ct = TestContext.Current.CancellationToken;

        var inner = new Mock<IAccountEntryRepository<CurrencyAccountEntry>>();
        inner.Setup(r => r.Get(_accountId, _jan, janEnd, It.IsAny<CancellationToken>()))
             .Returns(Array.Empty<CurrencyAccountEntry>().ToAsyncEnumerable());
        inner.Setup(r => r.Get(_accountId, _feb, febEnd, It.IsAny<CancellationToken>()))
             .Returns(new[] { feb1Entry }.ToAsyncEnumerable());

        var sut = CreateSut(inner.Object, BuildCache());
        var result = await sut.Get(_accountId, _jan, _feb.AddDays(27), ct).ToListAsync(ct);

        Assert.Single(result);
        Assert.Equal(10, result[0].EntryId);
    }

    [Fact]
    public async Task Get_EntryOnLastDayOfMonth_IncludedInRequest()
    {
        var jan31 = new DateTime(2025, 1, 31, 0, 0, 0, DateTimeKind.Utc);
        var janEnd = MonthBucket.MonthEndInclusive(_jan);
        var ct = TestContext.Current.CancellationToken;

        var inner = new Mock<IAccountEntryRepository<CurrencyAccountEntry>>();
        inner.Setup(r => r.Get(_accountId, _jan, janEnd, It.IsAny<CancellationToken>()))
             .Returns(new[] { Entry(5, jan31) }.ToAsyncEnumerable());

        var sut = CreateSut(inner.Object, BuildCache());
        var result = await sut.Get(_accountId, _jan, janEnd, ct).ToListAsync(ct);

        Assert.Single(result);
        Assert.Equal(5, result[0].EntryId);
    }

    // ── Horizon split ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Get_CrossingHorizon_SplitsIntoUncachedOlderAndBucketedRecent()
    {
        // Pin the "recent" window to exactly the first month inside the 12-month horizon so
        // GetBucketedEntries inflates exactly one bucket — otherwise we'd need stubs for every
        // month between horizonStart and endDate, which makes the test fragile.
        var horizon = MonthBucket.HorizonStart(12, DateTime.UtcNow);
        var horizonMonthEnd = MonthBucket.MonthEndInclusive(horizon);
        var recentDate = horizon.AddDays(5);   // a date in the first horizon month

        var oldDate = horizon.AddMonths(-2);   // 2 months before horizon: definitely uncached
        var olderEnd = horizon.AddTicks(-1);
        var ct = TestContext.Current.CancellationToken;

        var inner = new Mock<IAccountEntryRepository<CurrencyAccountEntry>>();
        inner.Setup(r => r.Get(_accountId, oldDate, olderEnd, It.IsAny<CancellationToken>()))
             .Returns(new[] { Entry(1, oldDate) }.ToAsyncEnumerable());
        inner.Setup(r => r.Get(_accountId, horizon, horizonMonthEnd, It.IsAny<CancellationToken>()))
             .Returns(new[] { Entry(2, recentDate) }.ToAsyncEnumerable());

        var sut = CreateSut(inner.Object, BuildCache(), options: _twelveMonthOptions);
        var result = await sut.Get(_accountId, oldDate, recentDate, ct).ToListAsync(ct);

        // Older portion passed straight through; recent portion inflated to full month.
        inner.Verify(r => r.Get(_accountId, oldDate, olderEnd, It.IsAny<CancellationToken>()), Times.Once);
        inner.Verify(r => r.Get(_accountId, horizon, horizonMonthEnd, It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal(2, result.Count);
        // Newest-first: recent (id=2) before old (id=1).
        Assert.Equal(2, result[0].EntryId);
        Assert.Equal(1, result[1].EntryId);
    }

    [Fact]
    public async Task Get_EntirelyBeforeHorizon_BypassesBucketCacheCompletely()
    {
        // 14-month-old range — beyond any 12-month horizon.
        var horizon = MonthBucket.HorizonStart(12, DateTime.UtcNow);
        var oldStart = horizon.AddMonths(-2);
        var oldEnd = horizon.AddTicks(-1);
        var ct = TestContext.Current.CancellationToken;

        var inner = new Mock<IAccountEntryRepository<CurrencyAccountEntry>>();
        inner.Setup(r => r.Get(_accountId, oldStart, oldEnd, It.IsAny<CancellationToken>()))
             .Returns(new[] { Entry(1, oldStart) }.ToAsyncEnumerable());

        var cache = BuildCache();
        var sut = CreateSut(inner.Object, cache, options: _twelveMonthOptions);

        await sut.Get(_accountId, oldStart, oldEnd, ct).ToListAsync(ct);
        await sut.Get(_accountId, oldStart, oldEnd, ct).ToListAsync(ct);

        // Both calls pass through — no bucket caching for old data.
        inner.Verify(r => r.Get(_accountId, oldStart, oldEnd, It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    // ── GetEntriesWithMinimumCount routes through the cached Get ─────────────────

    [Fact]
    public async Task GetEntriesWithMinimumCount_ZeroMinimum_UsesRangeBucketCache()
    {
        var janEnd = MonthBucket.MonthEndInclusive(_jan);

        var inner = new Mock<IAccountEntryRepository<CurrencyAccountEntry>>();
        inner.Setup(r => r.Get(_accountId, _jan, janEnd, It.IsAny<CancellationToken>()))
             .Returns(new[] { Entry(1, _jan.AddDays(10)) }.ToAsyncEnumerable());

        var cache = BuildCache();
        var sut = CreateSut(inner.Object, cache);

        var (entries, effectiveStart) = await sut.GetEntriesWithMinimumCount(_accountId, _jan, janEnd);

        Assert.Single(entries);
        Assert.Equal(_jan, effectiveStart);

        // Second call: full cache hit.
        await sut.GetEntriesWithMinimumCount(_accountId, _jan, janEnd);
        inner.Verify(r => r.Get(_accountId, _jan, janEnd, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetValueRange_OverlappingRequests_ShareOneMetadataFreeQuery()
    {
        var jan5 = _jan.AddDays(4);
        var jan10 = _jan.AddDays(9);
        var jan20 = _jan.AddDays(19);
        var jan25 = _jan.AddDays(24);
        var janEnd = MonthBucket.MonthEndInclusive(_jan);

        var inner = new Mock<IAccountEntryRepository<CurrencyAccountEntry>>();
        inner.Setup(r => r.GetValueRange(
                It.Is<IReadOnlyCollection<int>>(ids => ids.SequenceEqual(new[] { _accountId })),
                _jan,
                janEnd))
            .ReturnsAsync([Entry(2, jan20), Entry(1, jan10)]);

        var sut = CreateSut(inner.Object, BuildCache());

        var first = await sut.GetValueRange([_accountId], jan10, jan20);
        var second = await sut.GetValueRange([_accountId], jan5, jan25);

        Assert.Equal(2, first.Count);
        Assert.Equal(2, second.Count);
        inner.Verify(r => r.GetValueRange(
            It.IsAny<IReadOnlyCollection<int>>(), _jan, janEnd), Times.Once);
    }

    [Fact]
    public async Task GetEntriesWithMinimumCount_UsesCachedPostingDatesAndBucketedGet()
    {
        var jan5 = _jan.AddDays(4);
        var jan10 = _jan.AddDays(9);
        var jan20 = _jan.AddDays(19);
        var janEnd = MonthBucket.MonthEndInclusive(_jan);

        var inner = new Mock<IAccountEntryRepository<CurrencyAccountEntry>>();
        inner.Setup(r => r.GetPostingDates(_accountId))
             .ReturnsAsync([jan20, jan10, jan5]);
        inner.Setup(r => r.Get(_accountId, _jan, janEnd, It.IsAny<CancellationToken>()))
             .Returns(new[] { Entry(3, jan20), Entry(2, jan10), Entry(1, jan5) }.ToAsyncEnumerable());

        var cache = BuildCache();
        var sut = CreateSut(inner.Object, cache);

        var (entries, effectiveStart) = await sut.GetEntriesWithMinimumCount(
            _accountId, jan5, janEnd, minimumEntryCount: 2);

        Assert.Equal(3, entries.Count);
        Assert.Equal(jan5, effectiveStart);
        // Inner Get was inflated to full month.
        inner.Verify(r => r.Get(_accountId, _jan, janEnd, It.IsAny<CancellationToken>()), Times.Once);
        // Inner GetEntriesWithMinimumCount must NOT be called (decorator handles it).
        inner.Verify(r => r.GetEntriesWithMinimumCount(
            It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<int>()), Times.Never);
    }

    // ── GetRange (multi-account) ───────────────────────────────────────────────────

    [Fact]
    public async Task GetRange_MultiAccount_ComposesBothAccountsFromBuckets()
    {
        const int _accountId2 = 8;
        const int _userId2 = 43;

        var jan10 = _jan.AddDays(9);
        var jan20 = _jan.AddDays(19);
        var janEnd = MonthBucket.MonthEndInclusive(_jan);

        var resolver = new Mock<IAccountUserResolver>();
        resolver.Setup(r => r.GetUserId(_accountId, It.IsAny<CancellationToken>())).ReturnsAsync(_userId);
        resolver.Setup(r => r.GetUserId(_accountId2, It.IsAny<CancellationToken>())).ReturnsAsync(_userId2);

        var inner = new Mock<IAccountEntryRepository<CurrencyAccountEntry>>();
        inner.Setup(r => r.Get(_accountId, _jan, janEnd, It.IsAny<CancellationToken>()))
             .Returns(new[] { Entry(1, jan20) }.ToAsyncEnumerable());
        inner.Setup(r => r.Get(_accountId2, _jan, janEnd, It.IsAny<CancellationToken>()))
             .Returns(new[] { new CurrencyAccountEntry(_accountId2, 2, jan10, 200m, 200m) }.ToAsyncEnumerable());

        var sut = CreateSut(inner.Object, BuildCache(), resolver: resolver.Object);

        var result = await sut.GetRange(
            [_accountId, _accountId2], _jan, janEnd);

        Assert.Equal(2, result.Count);
        // Newest-first across both accounts.
        Assert.Equal(1, result[0].EntryId);
        Assert.Equal(2, result[1].EntryId);
    }

    [Fact]
    public async Task GetRange_SecondCall_ServedFromBuckets_InnerNotCalledAgain()
    {
        const int _accountId2 = 8;
        const int _userId2 = 43;
        var janEnd = MonthBucket.MonthEndInclusive(_jan);

        var resolver = new Mock<IAccountUserResolver>();
        resolver.Setup(r => r.GetUserId(_accountId, It.IsAny<CancellationToken>())).ReturnsAsync(_userId);
        resolver.Setup(r => r.GetUserId(_accountId2, It.IsAny<CancellationToken>())).ReturnsAsync(_userId2);

        var inner = new Mock<IAccountEntryRepository<CurrencyAccountEntry>>();
        inner.Setup(r => r.Get(_accountId, _jan, janEnd, It.IsAny<CancellationToken>()))
             .Returns(new[] { Entry(1, _jan.AddDays(5)) }.ToAsyncEnumerable());
        inner.Setup(r => r.Get(_accountId2, _jan, janEnd, It.IsAny<CancellationToken>()))
             .Returns(new[] { new CurrencyAccountEntry(_accountId2, 2, _jan.AddDays(10), 50m, 50m) }.ToAsyncEnumerable());

        var cache = BuildCache();
        var sut = CreateSut(inner.Object, cache, resolver: resolver.Object);

        await sut.GetRange([_accountId, _accountId2], _jan, janEnd);
        await sut.GetRange([_accountId, _accountId2], _jan, janEnd);

        inner.Verify(r => r.Get(_accountId, _jan, janEnd, It.IsAny<CancellationToken>()), Times.Once);
        inner.Verify(r => r.Get(_accountId2, _jan, janEnd, It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── Invalidation busts range buckets ──────────────────────────────────────────

    [Fact]
    public async Task Write_BustsBucketedRange_NextReadHitsInnerAgain()
    {
        var jan5 = _jan.AddDays(4);
        var jan15 = _jan.AddDays(14);
        var janEnd = MonthBucket.MonthEndInclusive(_jan);
        var ct = TestContext.Current.CancellationToken;

        var inner = new Mock<IAccountEntryRepository<CurrencyAccountEntry>>();
        inner.SetupSequence(r => r.Get(_accountId, _jan, janEnd))
             .Returns(new[] { Entry(1, jan5, 100m) }.ToAsyncEnumerable())
             .Returns(new[] { Entry(2, jan15, 200m), Entry(1, jan5, 200m) }.ToAsyncEnumerable());
        inner.Setup(r => r.Add(
                It.IsAny<CurrencyAccountEntry>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var cache = BuildCache();
        var sut = CreateSut(inner.Object, cache, invalidator: new CacheInvalidator(cache));

        var before = await sut.Get(_accountId, _jan, janEnd, ct).ToListAsync(ct); // miss → cached
        await sut.Add(Entry(2, jan15, 200m), recalculate: true, cancellationToken: ct); // write busts global:u42
        var after = await sut.Get(_accountId, _jan, janEnd, ct).ToListAsync(ct);  // miss again

        Assert.Single(before);
        Assert.Equal(2, after.Count);
        Assert.Equal(200m, after[0].Value);
    }

    // ── Unresolvable owner falls back to inner ────────────────────────────────────

    [Fact]
    public async Task Get_WhenOwnerUnresolved_PassesThroughUncached()
    {
        var ct = TestContext.Current.CancellationToken;

        var inner = new Mock<IAccountEntryRepository<CurrencyAccountEntry>>();
        inner.Setup(r => r.Get(_accountId, _jan, _feb, It.IsAny<CancellationToken>()))
             .Returns(new[] { Entry(1, _jan.AddDays(5)) }.ToAsyncEnumerable());

        var sut = CreateSut(inner.Object, BuildCache(), resolver: ResolverFor(_accountId, null));

        await sut.Get(_accountId, _jan, _feb, ct).ToListAsync(ct);
        await sut.Get(_accountId, _jan, _feb, ct).ToListAsync(ct);

        // Both calls fall through — no caching without a resolvable userId.
        inner.Verify(r => r.Get(_accountId, _jan, _feb, It.IsAny<CancellationToken>()), Times.Exactly(2));
    }
}